namespace Redpoint.CloudFramework.CLI
{
    using Microsoft.Extensions.Logging;
    using Redpoint.CommandLine;
    using System.CommandLine;
    using System.Threading.Tasks;
    using Redpoint.ProcessExecution;
    using System.Text;

    internal class GenerateHtmlFromMjml
    {
        internal class Options
        {
            public Option<FileInfo> Path = new Option<FileInfo>("--path", "The path to the MJML file.");
        }

        public static Command CreateCommand(ICommandBuilder builder)
        {
            return new Command("generate-html-from-mjml", "Generates a HTML file from an MJML file.");
        }

        internal class CommandInstance : ICommandInstance
        {
            private readonly ILogger<CommandInstance> _logger;
            private readonly IYarnInstallationService _yarnInstallationService;
            private readonly IProcessExecutor _processExecutor;
            private readonly Options _options;

            internal static readonly string[] _yarnInitArgs = new[] { "init", "-2" };
            internal static readonly string[] _yarnAddMjmlArgs = new[] { "add", "-D", "mjml" };
            internal static readonly string[] _yarnAddHtmlToTextArgs = new[] { "add", "-D", "@html-to/text-cli", "dom-serializer" };
            internal static readonly string[] _htmlToTextArgs = new[] { "run", "html-to-text", "--selectors[]", ":[0].selector=h1", ":[0].format=skip" };

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
                var inputPath = context.ParseResult.GetValueForOption(_options.Path);
                if (inputPath == null || !inputPath.Exists)
                {
                    _logger.LogError("Expected --input-path to exist.");
                    return 1;
                }

                // Install Yarn.
                var (exitCode, yarnCorepackShimPath) = await _yarnInstallationService.InstallYarnIfNeededAsync(context.GetCancellationToken()).ConfigureAwait(true);
                if (yarnCorepackShimPath == null)
                {
                    return exitCode;
                }

                // Create our directory where we will install the mjml tool.
                var mjmlInstallPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "rcf-mjml");
                Directory.CreateDirectory(mjmlInstallPath);

                // If Yarn isn't initialised in this directory, do it now.
                if (!File.Exists(Path.Combine(mjmlInstallPath, "yarn.lock")))
                {
                    exitCode = await _processExecutor.ExecuteAsync(
                        new ProcessSpecification
                        {
                            FilePath = yarnCorepackShimPath,
                            Arguments = _yarnInitArgs.Select(x => new LogicalProcessArgument(x)),
                            WorkingDirectory = mjmlInstallPath,
                        },
                        new YarnInstallCaptureSpecification(_logger),
                        context.GetCancellationToken()).ConfigureAwait(true);
                    if (exitCode != 0)
                    {
                        _logger.LogError("'yarn init -2' command failed; see above for output.");
                        return exitCode;
                    }
                }

                // If package.json doesn't have mjml, install it.
                if (!File.ReadAllText(Path.Combine(mjmlInstallPath, "package.json")).Contains(@"""mjml""", StringComparison.Ordinal))
                {
                    exitCode = await _processExecutor.ExecuteAsync(
                        new ProcessSpecification
                        {
                            FilePath = yarnCorepackShimPath,
                            Arguments = _yarnAddMjmlArgs.Select(x => new LogicalProcessArgument(x)),
                            WorkingDirectory = mjmlInstallPath,
                        },
                        new YarnInstallCaptureSpecification(_logger),
                        context.GetCancellationToken()).ConfigureAwait(true);
                    if (exitCode != 0)
                    {
                        _logger.LogError("'yarn add -D mjml' command failed; see above for output.");
                        return exitCode;
                    }
                }

                // If package.json doesn't have @html-to/text-cli, install it.
                if (!File.ReadAllText(Path.Combine(mjmlInstallPath, "package.json")).Contains(@"""@html-to/text-cli""", StringComparison.Ordinal))
                {
                    exitCode = await _processExecutor.ExecuteAsync(
                        new ProcessSpecification
                        {
                            FilePath = yarnCorepackShimPath,
                            Arguments = _yarnAddHtmlToTextArgs.Select(x => new LogicalProcessArgument(x)),
                            WorkingDirectory = mjmlInstallPath,
                        },
                        new YarnInstallCaptureSpecification(_logger),
                        context.GetCancellationToken()).ConfigureAwait(true);
                    if (exitCode != 0)
                    {
                        _logger.LogError("'yarn add -D @html-to/text-cli' command failed; see above for output.");
                        return exitCode;
                    }
                }

                // Execute mjml for the input and output paths.
                exitCode = await _processExecutor.ExecuteAsync(
                    new ProcessSpecification
                    {
                        FilePath = yarnCorepackShimPath,
                        Arguments = new[] { "run", "mjml", "-r", inputPath.FullName, "-o", inputPath.FullName + ".html" }.Select(x => new LogicalProcessArgument(x)),
                        WorkingDirectory = mjmlInstallPath,
                    },
                    CaptureSpecification.Passthrough,
                    context.GetCancellationToken()).ConfigureAwait(true);
                if (exitCode != 0)
                {
                    return exitCode;
                }

                // Execute html-to-text on the output HTML, so that we can have a text version as well.
                var textStringBuilder = new StringBuilder();
                exitCode = await _processExecutor.ExecuteAsync(
                    new ProcessSpecification
                    {
                        FilePath = yarnCorepackShimPath,
                        Arguments = _htmlToTextArgs.Select(x => new LogicalProcessArgument(x)),
                        WorkingDirectory = mjmlInstallPath,
                    },
                    new HtmlToTextCaptureSpecification(File.ReadAllText(inputPath.FullName + ".html"), textStringBuilder),
                    context.GetCancellationToken()).ConfigureAwait(true);
                File.WriteAllText(inputPath.FullName + ".txt", textStringBuilder.ToString());
                return exitCode;
            }
        }
    }
}
