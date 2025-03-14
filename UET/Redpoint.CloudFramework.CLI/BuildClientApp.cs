namespace Redpoint.CloudFramework.CLI
{
    using Microsoft.Extensions.Logging;
    using Redpoint.CommandLine;
    using System.CommandLine;
    using System.Threading.Tasks;
    using Redpoint.ProcessExecution;

    internal class BuildClientApp
    {
        internal class Options
        {
            public Option<DirectoryInfo> Path = new Option<DirectoryInfo>(
                "--app-path",
                "The path to the client application. This directory should have a package.json file in it.");

            public Option<string> Configuration = new Option<string>(
                "--configuration",
                "The build configuration; should be set to $(Configuration) from MSBuild.");
        }

        public static Command CreateCommand(ICommandBuilder builder)
        {
            return new Command("build-client-app", "Builds a TypeScript-based client app, installing all required dependencies as needed.");
        }

        internal class CommandInstance : ICommandInstance
        {
            private readonly ILogger<CommandInstance> _logger;
            private readonly IYarnInstallationService _yarnInstallationService;
            private readonly IProcessExecutor _processExecutor;
            private readonly Options _options;

            private static readonly string[] _openapiGenerateArgs = new[] { "run", "openapi", "--input", "openapi.json", "--output", "src/api", "-c", "fetch" };
            private static readonly string[] _webpackProductionBuildArgs = new[] { "run", "webpack", "--progress", "--mode", "production" };
            private static readonly string[] _yarnInstallArgs = new[] { "install", "--json" };

            public CommandInstance(
                ILogger<CommandInstance> logger,
                IYarnInstallationService yarnInstallationService,
                IProcessExecutor processExecutor,
                Options options)
            {
                _logger = logger;
                _yarnInstallationService = yarnInstallationService;
                _processExecutor = processExecutor;
                _options = options;
            }

            public async Task<int> ExecuteAsync(ICommandInvocationContext context)
            {
                var appPath = context.ParseResult.GetValueForOption(_options.Path);
                if (appPath == null || !appPath.Exists || !File.Exists(Path.Combine(appPath.FullName, "package.json")))
                {
                    _logger.LogError("Expected --app-path to exist and contain a package.json file.");
                    return 1;
                }
                var configuration = context.ParseResult.GetValueForOption(_options.Configuration) ?? string.Empty;

                // Install Yarn.
                var (exitCode, yarnCorepackShimPath) = await _yarnInstallationService.InstallYarnIfNeededAsync(context.GetCancellationToken()).ConfigureAwait(true);
                if (yarnCorepackShimPath == null)
                {
                    return exitCode;
                }

                // Run 'yarn install' to install dependencies.
                exitCode = await _processExecutor.ExecuteAsync(
                    new ProcessSpecification
                    {
                        FilePath = yarnCorepackShimPath,
                        Arguments = _yarnInstallArgs.Select(x => new LogicalProcessArgument(x)),
                        WorkingDirectory = appPath.FullName,
                    },
                    new YarnInstallCaptureSpecification(_logger),
                    context.GetCancellationToken()).ConfigureAwait(true);
                if (exitCode != 0)
                {
                    _logger.LogError("'yarn install' command failed; see above for output.");
                    return exitCode;
                }

                // If an openapi.json file exists in the root of the application path,
                // automatically generate the TypeScript API for it.
                foreach (var jsonFile in new DirectoryInfo(appPath.FullName).EnumerateFiles("*.json"))
                {
                    if (string.Equals(jsonFile.Name, "openapi.json", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogInformation($"Generating TypeScript API for OpenAPI document '{jsonFile.Name}'...");
                        exitCode = await _processExecutor.ExecuteAsync(
                            new ProcessSpecification
                            {
                                FilePath = yarnCorepackShimPath,
                                Arguments = _openapiGenerateArgs.Select(x => new LogicalProcessArgument(x)),
                                WorkingDirectory = appPath.FullName,
                            },
                            CaptureSpecification.Passthrough,
                            context.GetCancellationToken()).ConfigureAwait(true);
                        if (exitCode != 0)
                        {
                            _logger.LogError("'yarn run openapi' command failed; see above for output.");
                            return exitCode;
                        }
                    }
                    else if (jsonFile.Name.StartsWith("openapi.", StringComparison.OrdinalIgnoreCase))
                    {
                        var versionName = jsonFile.Name.Substring("openapi.".Length);
                        versionName = versionName.Substring(0, versionName.Length - ".json".Length);
                        _logger.LogInformation($"Generating TypeScript API for OpenAPI document '{jsonFile.Name}', version '{versionName}'...");
                        exitCode = await _processExecutor.ExecuteAsync(
                            new ProcessSpecification
                            {
                                FilePath = yarnCorepackShimPath,
                                Arguments = new LogicalProcessArgument[]
                                {
                                    "run",
                                    "openapi",
                                    "--input",
                                    jsonFile.Name,
                                    "--output",
                                    $"src/api/{versionName}",
                                    "-c",
                                    "fetch"
                                },
                                WorkingDirectory = appPath.FullName,
                            },
                            CaptureSpecification.Passthrough,
                            context.GetCancellationToken()).ConfigureAwait(true);
                        if (exitCode != 0)
                        {
                            _logger.LogError("'yarn run openapi' command failed; see above for output.");
                            return exitCode;
                        }
                    }
                }

                if (configuration == "Release")
                {
                    // If we're building for Release, build the production version of the application.
                    return await _processExecutor.ExecuteAsync(
                        new ProcessSpecification
                        {
                            FilePath = yarnCorepackShimPath,
                            Arguments = _webpackProductionBuildArgs.Select(x => new LogicalProcessArgument(x)),
                            WorkingDirectory = appPath.FullName,
                            EnvironmentVariables = File.Exists(Path.Combine(appPath.FullName, "tsconfig.webpack.json")) ? new Dictionary<string, string>
                            {
                                { "TS_NODE_PROJECT", "tsconfig.webpack.json" }
                            } : null
                        },
                        CaptureSpecification.Passthrough,
                        context.GetCancellationToken()).ConfigureAwait(true);
                }
                else
                {
                    // If we're building for Debug, then Redpoint.CloudFramework will handle running
                    // Webpack in watch mode.
                    return 0;
                }
            }
        }
    }
}
