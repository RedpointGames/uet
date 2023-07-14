namespace UET.Commands.Internal.CreateGitHubRelease
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.ProgressMonitor;
    using System;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Net.Http.Headers;
    using System.Text.Json;
    using System.Text.Json.Serialization.Metadata;
    using System.Threading.Tasks;
    using System.Web;
    using UET.Commands.Internal.GenerateJsonSchema;

    internal class CreateGitHubReleaseCommand
    {
        internal class Options
        {
            public Option<string> Version;
            public Option<string[]> Files;
            public Option<bool> SchemaOnly;

            public Options()
            {
                Version = new Option<string>(
                    "--version",
                    "The version number for the release.")
                {
                    IsRequired = true,
                };
                Files = new Option<string[]>(
                    "--file",
                    "One or more files to include in the release as assets. These can be paths on their own, or in the form of name=label=path.");
                SchemaOnly = new Option<bool>(
                    "--schema-only",
                    "Only upload the generated schema, and do not generate a release on the main repository.");
            }
        }

        public static Command CreateCreateGitHubReleaseCommand()
        {
            var options = new Options();
            var command = new Command("create-github-release");
            command.AddAllOptions(options);
            command.AddCommonHandler<CreateGitHubReleaseCommandInstance>(options, services =>
            {
                services.AddSingleton<IReleaseUploader, DefaultReleaseUploader>();
                services.AddSingleton<ISchemaUploader, DefaultSchemaUploader>();
                services.AddSingleton<IJsonSchemaGenerator, DefaultJsonSchemaGenerator>();
            });
            return command;
        }

        private class CreateGitHubReleaseCommandInstance : ICommandInstance
        {
            private readonly ILogger<CreateGitHubReleaseCommandInstance> _logger;
            private readonly Options _options;
            private readonly IReleaseUploader _releaseUploader;
            private readonly ISchemaUploader _schemaUploader;

            public CreateGitHubReleaseCommandInstance(
                ILogger<CreateGitHubReleaseCommandInstance> logger,
                Options options,
                IReleaseUploader releaseUploader,
                ISchemaUploader schemaUploader)
            {
                _logger = logger;
                _options = options;
                _releaseUploader = releaseUploader;
                _schemaUploader = schemaUploader;
            }

            public async Task<int> ExecuteAsync(InvocationContext context)
            {
                var version = context.ParseResult.GetValueForOption(_options.Version)!;
                var files = (context.ParseResult.GetValueForOption(_options.Files) ?? Array.Empty<string>()).Select(x =>
                {
                    if (x.Contains("="))
                    {
                        var c = x.Split("=", 3);
                        return (
                            name: c[0],
                            label: c[1],
                            path: new FileInfo(c[2])
                        );
                    }
                    else
                    {
                        var fi = new FileInfo(x);
                        return (
                            name: fi.Name,
                            label: fi.Name,
                            path: fi
                        );
                    }
                }).ToArray();
                var schemaOnly = context.ParseResult.GetValueForOption(_options.SchemaOnly);

                var pat = Environment.GetEnvironmentVariable("GITHUB_RELEASES_PAT");
                if (string.IsNullOrWhiteSpace(pat))
                {
                    _logger.LogError("Expected environment variable GITHUB_RELEASES_PAT to be set (this is an internal command).");
                    return 1;
                }

                if (files.Length == 0 && !schemaOnly)
                {
                    _logger.LogError("Expected at least one file to include in the GitHub release.");
                    return 1;
                }

                using (var client = new HttpClient())
                {
                    client.Timeout = Timeout.InfiniteTimeSpan;

                    client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("UET", null));
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", pat);
                    client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");

                    if (!schemaOnly)
                    {
                        await _releaseUploader.CreateVersionReleaseAsync(context, version, files, client);
                        await _releaseUploader.UpdateLatestReleaseAsync(context, version, files, client);
                    }
                    await _schemaUploader.UpdateSchemaRepositoryAsync(version, client, context.GetCancellationToken());

                    _logger.LogInformation($"GitHub release process complete.");
                    return 0;
                }
            }
        }
    }
}
