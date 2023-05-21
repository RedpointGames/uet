namespace Redpoint.UET.BuildPipeline.Executors.GitLab
{
    using Microsoft.Extensions.Logging;
    using Redpoint.UET.BuildPipeline.BuildGraph;
    using Redpoint.UET.BuildPipeline.Executors;
    using Redpoint.UET.BuildPipeline.Executors.BuildServer;
    using Redpoint.UET.BuildPipeline.Executors.Engine;
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
            IWorkspaceProvider workspaceProvider) : base(
                baseLogger,
                buildGraphExecutor,
                engineWorkspaceProvider,
                workspaceProvider)
        {
            _logger = logger;
        }

        protected override async Task EmitBuildServerSpecificFileAsync(
            BuildSpecification buildSpecification,
            BuildServerPipeline buildServerPipeline,
            string buildServerOutputFilePath)
        {
            _logger.LogInformation("Generating .gitlab-ci.yml content...");

            var file = new GitLabFile
            {
                Stages = buildServerPipeline.Stages.ToList(),
                Variables = new Dictionary<string, string>(),
                Jobs = new Dictionary<string, GitLabJob>(),
            };
            foreach (var kv in buildServerPipeline.GlobalEnvironmentVariables)
            {
                file.Variables[kv.Key] = kv.Value;
            }
            file.Variables["GIT_STRATEGY"] = "none";

            foreach (var sourceJob in buildServerPipeline.Jobs.Values)
            {
                var job = new GitLabJob();

                if (sourceJob.Platform == BuildServerJobPlatform.Windows)
                {
                    job.Tags = new[] { "buildgraph-windows" };
                }
                else if (sourceJob.Platform == BuildServerJobPlatform.Mac)
                {
                    job.Tags = new[] { "buildgraph-mac" };
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

                file.Jobs.Add(sourceJob.Name, job);
            }

            using (var stream = new StreamWriter(buildServerOutputFilePath))
            {
                var serializer = new SerializerBuilder().Build();
                var yaml = serializer.Serialize(file);
                await stream.WriteLineAsync(yaml);
            }
        }
    }
}