namespace Redpoint.Uet.BuildPipeline.Executors.Jenkins
{
    using Microsoft.Extensions.Logging;
    using Redpoint.Uet.BuildPipeline.Executors;
    using Redpoint.Uet.BuildPipeline.Executors.BuildServer;
    using System;
    using System.Globalization;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Net.Http.Json;
    using System.Text;
    using System.Threading.Tasks;
    using System.Web;
    using System.Xml;

    public class JenkinsBuildExecutor : BuildServerBuildExecutor
    {
        private readonly ILogger<JenkinsBuildExecutor> _logger;
        private static readonly HttpClient _httpClient = new();
        private readonly Uri? _gitUri;
        private readonly string _gitBranch;

        public JenkinsBuildExecutor(
            IServiceProvider serviceProvider,
            ILogger<JenkinsBuildExecutor> logger,
            string buildServerOutputFilePath,
            Uri? gitUri,
            string gitBranch) : base(
                serviceProvider,
                buildServerOutputFilePath)
        {
            _logger = logger;

            _gitUri = gitUri;
            if (_gitUri == null)
            {
                var gitUriEnv = Environment.GetEnvironmentVariable("GIT_URL");
                if (!string.IsNullOrWhiteSpace(gitUriEnv))
                {
                    Uri.TryCreate(gitUriEnv, UriKind.Absolute, out _gitUri);
                }
            }

            _gitBranch = gitBranch;
            if (string.IsNullOrWhiteSpace(_gitBranch))
            {
                _gitBranch = Environment.GetEnvironmentVariable("GIT_LOCAL_BRANCH") ?? string.Empty;
            }
        }

        public override string DiscoverPipelineId()
        {
            // TODO: This is not defined when executing locally, investigate consequence and implement alternative if needed.
            return Environment.GetEnvironmentVariable("BUILD_TAG") ?? string.Empty;
        }

        protected override async Task EmitBuildServerSpecificFileAsync(BuildSpecification buildSpecification, BuildServerPipeline buildServerPipeline, string buildServerOutputFilePath)
        {
            ArgumentNullException.ThrowIfNull(buildServerPipeline);

            var prerequisitesPassed = true;
            if (_gitUri == null)
            {
                _logger.LogError("Jenkins executor requires a valid Git URL. Specify using command-line argument or 'GIT_URL' environment variable.");
                prerequisitesPassed = false;
            }

            if (string.IsNullOrWhiteSpace(_gitBranch))
            {
                _logger.LogError("Jenkins executor requires a valid Git branch. Specify using command-line argument or 'GIT_LOCAL_BRANCH' environment variable.");
                prerequisitesPassed = false;
            }

            string? controllerUri = Environment.GetEnvironmentVariable("UET_JENKINS_CONTROLLER_URL");
            if (string.IsNullOrWhiteSpace(controllerUri))
            {
                _logger.LogError("Jenkins executor requires a valid Jenkins controller URL. Specify using 'UET_JENKINS_CONTROLLER_URL' environment variable.");
                prerequisitesPassed = false;
            }

            string? authenticationToken = Environment.GetEnvironmentVariable("UET_JENKINS_AUTH");
            if (string.IsNullOrWhiteSpace(authenticationToken))
            {
                _logger.LogError("Jenkins executor requires a valid authorization token for Jenkins controller. Specify using 'UET_JENKINS_AUTH' environment variable (example: your-user-name:apiToken)");
                prerequisitesPassed = false;
            }

            if (!prerequisitesPassed)
            {
                throw new BuildPipelineExecutionFailureException("One or more prerequisite checks have failed, fix and try again.");
            }

            _httpClient.BaseAddress = new Uri(controllerUri!);
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes(authenticationToken!)));

            foreach (var stage in buildServerPipeline.Stages)
            {
                _logger.LogInformation($"Creating and executing Jenkins jobs for stage '{stage}'");

                var sourceJobsThisStage = buildServerPipeline.Jobs.Where(x => x.Value.Stage == stage).ToDictionary();
                var jobs = new Dictionary<string, JenkinsJob>();
                foreach (var sourceJob in sourceJobsThisStage)
                {
                    var jobName = sourceJob.Key;
                    var jobData = sourceJob.Value;

                    // Job config data.
                    using var stringWriter = new StringWriter();
                    using (var xmlWriter = XmlWriter.Create(stringWriter, new XmlWriterSettings
                    {
                        OmitXmlDeclaration = true,
                        ConformanceLevel = ConformanceLevel.Fragment,
                    }))
                    {
                        xmlWriter.WriteStartElement("project");

                        xmlWriter.WriteStartElement("description");
                        xmlWriter.WriteString("This job was generated by UET.");
                        xmlWriter.WriteEndElement(); // description

                        xmlWriter.WriteStartElement("scm");
                        xmlWriter.WriteAttributeString("class", "hudson.scm.NullSCM");
                        xmlWriter.WriteEndElement(); // scm

                        var nodeLabel = string.Empty;
                        if (jobData.Agent.Platform == BuildServerJobPlatform.Windows)
                        {
                            nodeLabel = "buildgraph-windows";
                        }
                        else if (jobData.Agent.Platform == BuildServerJobPlatform.Mac)
                        {
                            nodeLabel = "buildgraph-mac";
                        }
                        else if (jobData.Agent.Platform == BuildServerJobPlatform.Meta)
                        {
                            // Don't emit this job.
                            continue;
                        }
                        else
                        {
                            throw new InvalidOperationException("Unsupported platform in Jenkins generation!");
                        }

                        xmlWriter.WriteStartElement("assignedNode");
                        xmlWriter.WriteString(nodeLabel);
                        xmlWriter.WriteEndElement(); // assignedNode

                        var buildCommandString = string.Empty;
                        foreach (var kv in jobData.EnvironmentVariables)
                        {
                            buildCommandString += $"$env:{kv.Key}=\'{kv.Value}\'\n";
                        }
                        buildCommandString += $"$env:UET_GIT_URL=\'{_gitUri}\'\n";
                        buildCommandString += $"$env:UET_GIT_REF=\'{_gitBranch}\'\n";

                        buildCommandString += jobData.Script("jenkins");

                        xmlWriter.WriteStartElement("builders");
                        xmlWriter.WriteStartElement("hudson.plugins.powershell.PowerShell");
                        xmlWriter.WriteAttributeString("plugin", "powershell@2.2");
                        xmlWriter.WriteStartElement("command");
                        xmlWriter.WriteString(buildCommandString);
                        xmlWriter.WriteEndElement(); // command
                        xmlWriter.WriteStartElement("stopOnError");
                        xmlWriter.WriteString("true");
                        xmlWriter.WriteEndElement(); // stopOnError
                        xmlWriter.WriteEndElement(); // PowerShell
                        xmlWriter.WriteEndElement(); // builders

                        xmlWriter.WriteEndElement(); // project
                    }

                    // Create or update Jenkins job depending on whether it exists.
                    var uriBuilder = new UriBuilder(_httpClient.BaseAddress + $"job/{jobName}/api/json");
                    using var checkResponse = await _httpClient.GetAsync(uriBuilder.Uri).ConfigureAwait(false);
                    if (checkResponse.IsSuccessStatusCode)
                    {
                        // Update existing job.
                        uriBuilder = new UriBuilder(_httpClient.BaseAddress + $"job/{jobName}/config.xml");

                        using StringContent xmlContent = new(stringWriter.ToString(), Encoding.UTF8, "text/xml");

                        using var updateJobResponse = await _httpClient.PostAsync(uriBuilder.Uri, xmlContent).ConfigureAwait(false);
                        if (!updateJobResponse.IsSuccessStatusCode)
                        {
                            throw new BuildPipelineExecutionFailureException($"Could not update Jenkins job '{jobName}'.");
                        }
                    }
                    else
                    {
                        // Create new job.
                        var query = HttpUtility.ParseQueryString(string.Empty);
                        query["name"] = jobName;
                        uriBuilder = new UriBuilder(_httpClient.BaseAddress + "createItem")
                        {
                            Query = query.ToString()
                        };

                        using StringContent xmlContent = new(stringWriter.ToString(), Encoding.UTF8, "application/xml");

                        using var createJobResponse = await _httpClient.PostAsync(uriBuilder.Uri, xmlContent).ConfigureAwait(false);
                        if (!createJobResponse.IsSuccessStatusCode)
                        {
                            throw new BuildPipelineExecutionFailureException($"Could not create Jenkins job '{jobName}'.");
                        }
                    }

                    jobs.Add(jobName, new JenkinsJob());

                    // Submit to build queue.
                    uriBuilder = new UriBuilder(_httpClient.BaseAddress + $"job/{jobName}/build");
                    using var buildResponse = await _httpClient.PostAsync(uriBuilder.Uri, null).ConfigureAwait(false);
                    if (!buildResponse.IsSuccessStatusCode)
                    {
                        throw new BuildPipelineExecutionFailureException($"Could not enqueue Jenkins job '{jobName}'.");
                    }
                    jobs[jobName].QueueUri = buildResponse.Headers.Location;
                    jobs[jobName].Status = JenkinsJobStatus.Queued;
                }

                while (jobs.Any(job => job.Value.Status == JenkinsJobStatus.Queued || job.Value.Status == JenkinsJobStatus.Executing))
                {
                    // Poll build queue for jobs that are starting execution.
                    foreach (var job in jobs.Where(job => job.Value.Status == JenkinsJobStatus.Queued))
                    {
                        var uriBuilder = new UriBuilder(job.Value.QueueUri + "api/json");
                        using var response = await _httpClient.GetAsync(uriBuilder.Uri).ConfigureAwait(false);
                        response.EnsureSuccessStatusCode();
                        var queueItem = await response.Content.ReadFromJsonAsync(JenkinsJsonSourceGenerationContext.Default.JenkinsQueueItem).ConfigureAwait(false);
                        ArgumentNullException.ThrowIfNull(queueItem);

                        if (queueItem.Cancelled ?? false)
                        {
                            throw new BuildPipelineExecutionFailureException($"Queued job '{job.Key}' was cancelled.");
                        }

                        if (queueItem.Executable != null)
                        {
                            job.Value.ExecutionUri = new Uri(queueItem.Executable.Url);
                            job.Value.Status = JenkinsJobStatus.Executing;
                        }
                    }

                    // Poll executing builds for progress.
                    foreach (var job in jobs.Where(job => job.Value.Status == JenkinsJobStatus.Executing))
                    {
                        var query = HttpUtility.ParseQueryString(string.Empty);
                        query["start"] = job.Value.ExecutionLogByteOffset.ToString(CultureInfo.InvariantCulture);
                        var uriBuilder = new UriBuilder(job.Value.ExecutionUri + "logText/progressiveText")
                        {
                            Query = query.ToString()
                        };
                        using var progressResponse = await _httpClient.GetAsync(uriBuilder.Uri).ConfigureAwait(false);
                        progressResponse.EnsureSuccessStatusCode();

                        // Get offset for the next log query, while at the same time check if we received any new log data.
                        int newByteOffset = int.Parse(progressResponse.Headers.GetValues("X-Text-Size").First(), CultureInfo.InvariantCulture);
                        if (newByteOffset != job.Value.ExecutionLogByteOffset)
                        {
                            job.Value.ExecutionLogByteOffset = newByteOffset;

                            var newLogText = await progressResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                            foreach (var line in newLogText.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                            {
                                LogLevel logLevel = line.Contains("FAILURE", StringComparison.OrdinalIgnoreCase) ? LogLevel.Error : LogLevel.Information;
                                _logger.Log(logLevel, $"[Remote: {job.Key}] " + line);
                            }
                        }

                        // Check if build is still in progress, header will disappear when build is complete.
                        bool buildStillInProgress = progressResponse.Headers.TryGetValues("X-More-Data", out var values) && bool.Parse(values.FirstOrDefault() ?? bool.FalseString);
                        if (!buildStillInProgress)
                        {
                            uriBuilder = new UriBuilder(job.Value.ExecutionUri + "api/json");
                            using var resultResponse = await _httpClient.GetAsync(uriBuilder.Uri).ConfigureAwait(false);
                            resultResponse.EnsureSuccessStatusCode();
                            var buildInfo = await resultResponse.Content.ReadFromJsonAsync(JenkinsJsonSourceGenerationContext.Default.JenkinsBuildInfo).ConfigureAwait(false);
                            ArgumentNullException.ThrowIfNull(buildInfo);

                            job.Value.Status = buildInfo.Result.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase) ? JenkinsJobStatus.Succeeded : JenkinsJobStatus.Failed;
                        }
                    }

                    // Don't poll too frequently.
                    await Task.Delay(1000).ConfigureAwait(false);
                }

                // Don't bother starting the next stage if a prerequisite job has failed.
                if (jobs.Any(job => job.Value.Status == JenkinsJobStatus.Failed))
                {
                    throw new BuildPipelineExecutionFailureException("A job has failed, aborting build process.");
                }
            }

            // TODO: Stuff when all jobs have finished?
        }
    }
}
