﻿namespace Redpoint.Uet.BuildPipeline.Executors.GitLab
{
    using Microsoft.Extensions.Logging;
    using Redpoint.Uet.BuildPipeline.BuildGraph;
    using Redpoint.Uet.BuildPipeline.Executors;
    using Redpoint.Uet.BuildPipeline.Executors.BuildServer;
    using Redpoint.Uet.BuildPipeline.Executors.Engine;
    using Redpoint.Uet.Configuration;
    using Redpoint.Uet.Core.Permissions;
    using Redpoint.Uet.Workspace;
    using System.Threading.Tasks;
    using YamlDotNet.Serialization;

    public class GitLabBuildExecutor : BuildServerBuildExecutor
    {
        private readonly ILogger<GitLabBuildExecutor> _logger;

        public GitLabBuildExecutor(
            IServiceProvider serviceProvider,
            ILogger<GitLabBuildExecutor> logger,
            string buildServerOutputFilePath) : base(
                serviceProvider,
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
            ArgumentNullException.ThrowIfNull(buildServerPipeline);

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
                job.Variables = new Dictionary<string, string>(sourceJob.EnvironmentVariables);
                job.Needs = sourceJob.Agent.IsManual ? new List<string>() : sourceJob.Needs.ToList();

                // Ensure that older jobs are stopped when replaced with newer ones.
                job.Interruptible = true;

                if (sourceJob.Agent.Platform == BuildServerJobPlatform.Windows)
                {
                    job.Tags = new List<string> { "buildgraph-windows" };
                }
                else if (sourceJob.Agent.Platform == BuildServerJobPlatform.Mac)
                {
                    job.Tags = new List<string> { "buildgraph-mac" };
                }
                else if (sourceJob.Agent.Platform == BuildServerJobPlatform.Meta)
                {
                    // Don't emit this job.
                    continue;
                }
                else
                {
                    throw new InvalidOperationException("Unsupported platform in GitLab generation!");
                }

                job.Tags.AddRange(sourceJob.Agent.BuildMachineTags);

                if (sourceJob.Agent.IsManual)
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
                        Paths = sourceJob.ArtifactPaths.ToArray(),
                    };
                    if (sourceJob.ArtifactJUnitReportPath != null)
                    {
                        job.Artifacts.Reports = new GitLabJobArtifactsReports
                        {
                            Junit = sourceJob.ArtifactJUnitReportPath,
                        };
                    }
                }

                job.Script = sourceJob.Script("gitlab");
                if (sourceJob.AfterScript != null)
                {
                    job.AfterScript = new[] { sourceJob.AfterScript.Trim().Replace("\r\n", "\n", StringComparison.Ordinal) };
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
                await stream.WriteLineAsync(yaml).ConfigureAwait(false);
            }
        }
    }
}