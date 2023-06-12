namespace Redpoint.UET.BuildPipeline.Executors.GitLab
{
    using Microsoft.Extensions.Logging;
    using Redpoint.UET.BuildPipeline.BuildGraph;
    using Redpoint.UET.BuildPipeline.Executors;
    using Redpoint.UET.BuildPipeline.Executors.BuildServer;
    using Redpoint.UET.BuildPipeline.Executors.Engine;
    using Redpoint.UET.Configuration;
    using Redpoint.UET.Workspace;
    using System.Threading.Tasks;
    using YamlDotNet.Serialization;

    public class GitLabBuildExecutor : BuildServerBuildExecutor
    {
        private readonly ILogger<GitLabBuildExecutor> _logger;

        public GitLabBuildExecutor(
            ILogger<GitLabBuildExecutor> logger,
            ILogger<BuildServerBuildExecutor> baseLogger,
            IBuildGraphExecutor buildGraphExecutor,
            IEngineWorkspaceProvider engineWorkspaceProvider,
            IDynamicWorkspaceProvider workspaceProvider,
            IGlobalArgsProvider? globalArgsProvider,
            string buildServerOutputFilePath) : base(
                baseLogger,
                buildGraphExecutor,
                engineWorkspaceProvider,
                workspaceProvider,
                globalArgsProvider,
                buildServerOutputFilePath)
        {
            _logger = logger;
        }

        public override string DiscoverPipelineId()
        {
            return System.Environment.GetEnvironmentVariable("CI_PIPELINE_ID") ?? string.Empty;
        }

        protected override async Task EmitBuildServerSpecificFileAsync(
            BuildSpecification buildSpecification,
            BuildServerPipeline buildServerPipeline,
            string buildServerOutputFilePath)
        {
            _logger.LogInformation("Generating .gitlab-ci.yml content...");

            var file = new Dictionary<string, object>
            {
                { "stages", buildServerPipeline.Stages.ToList() }
            };
            var variables = new Dictionary<string, string>();
            file.Add("variables", variables);
            foreach (var kv in buildServerPipeline.GlobalEnvironmentVariables)
            {
                variables[kv.Key] = kv.Value;
            }
            variables["GIT_STRATEGY"] = "none";

            foreach (var sourceJob in buildServerPipeline.Jobs.Values)
            {
                var job = new GitLabJob();
                job.Stage = sourceJob.Stage;
                job.Variables = sourceJob.EnvironmentVariables;
                job.Needs = sourceJob.Needs.ToList();

                if (sourceJob.Platform == BuildServerJobPlatform.Windows)
                {
                    job.Tags = new List<string> { "buildgraph-windows" };
                }
                else if (sourceJob.Platform == BuildServerJobPlatform.Mac)
                {
                    job.Tags = new List<string> { "buildgraph-mac" };
                }
                else if (sourceJob.Platform == BuildServerJobPlatform.Meta)
                {
                    // Don't emit this job.
                    continue;
                }
                else
                {
                    throw new InvalidOperationException("Unsupported platform in GitLab generation!");
                }

                if (sourceJob.IsManual)
                {
                    job.Rules = new GitLabJobRule[]
                    {
                        new GitLabJobRule
                        {
                            If = @"$CI == ""true"" && $GITLAB_INTENT == ""manual""",
                            When = "manual",
                        }
                    };
                }
                else
                {
                    job.Rules = new GitLabJobRule[]
                    {
                        new GitLabJobRule
                        {
                            If = @"$CI == ""true"" && $GITLAB_INTENT != ""manual""",
                        }
                    };
                }

                if (sourceJob.ArtifactPaths != null)
                {
                    job.Artifacts = new GitLabJobArtifacts
                    {
                        When = "always",
                        Paths = sourceJob.ArtifactPaths,
                    };
                    if (sourceJob.ArtifactJUnitReportPath != null)
                    {
                        job.Artifacts.Reports = new GitLabJobArtifactsReports
                        {
                            Junit = sourceJob.ArtifactJUnitReportPath,
                        };
                    }
                }

                job.Script = sourceJob.Script;
                if (sourceJob.AfterScript != null)
                {
                    job.AfterScript = new[] { sourceJob.AfterScript.Trim().Replace("\r\n", "\n") };
                }

                file.Add(sourceJob.Name, job);
            }

            using (var stream = new StreamWriter(buildServerOutputFilePath))
            {
                var aotContext = new GitLabYamlStaticContext();
                var serializer = new StaticSerializerBuilder(aotContext)
                    .WithQuotingNecessaryStrings()
                    .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
                    .Build();
                var yaml = serializer.Serialize(file);
                await stream.WriteLineAsync(yaml);
            }
        }
    }
}