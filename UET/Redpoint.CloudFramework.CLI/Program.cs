extern alias RDCommandLine;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RDCommandLine::Microsoft.Extensions.Logging.Console;
using Redpoint.CloudFramework.CLI;
using Redpoint.CommandLine;
using Redpoint.Logging.SingleLine;
using Redpoint.PathResolution;
using Redpoint.ProcessExecution;
using System.CommandLine;

Crayon.Output.Disable();

// Turn off all Node.js deprecation warnings. They're not actionable.
Environment.SetEnvironmentVariable("NODE_NO_WARNINGS", "1");

var rootCommand = CommandLineBuilder.NewBuilder()
    .AddGlobalRuntimeServices((builder, services) =>
    {
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddSingleLineConsoleFormatter(options =>
            {
                options.OmitLogPrefix = false;
                options.ColorBehavior = LoggerColorBehavior.Disabled;
            });
            builder.AddSingleLineConsole();
        });
        services.AddProcessExecution();
        services.AddPathResolution();
        services.AddSingleton<IYarnInstallationService, DefaultYarnInstallationService>();
    })
    .SetGlobalExecutionHandler(async (sp, executeCommand) =>
    {
        var logger = sp.GetRequiredService<ILogger<Program>>();
        try
        {
            return await executeCommand().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Uncaught exception during command execution: {ex}");
            return 1;
        }
    })
    .AddCommand<BuildClientApp.CommandInstance, BuildClientApp.Options>(BuildClientApp.CreateCommand)
    .AddCommand<GenerateOpenApiJson.CommandInstance, GenerateOpenApiJson.Options>(GenerateOpenApiJson.CreateCommand)
    .AddCommand<GenerateHtmlFromMjml.CommandInstance, GenerateHtmlFromMjml.Options>(GenerateHtmlFromMjml.CreateCommand)
    .AddCommand<GenerateStronglyTypedLanguageFiles.CommandInstance, GenerateStronglyTypedLanguageFiles.Options>(GenerateStronglyTypedLanguageFiles.CreateCommand)
    .Build();

var exitCode = await rootCommand.InvokeAsync(args).ConfigureAwait(false);
await Console.Out.FlushAsync().ConfigureAwait(false);
await Console.Error.FlushAsync().ConfigureAwait(false);
Environment.Exit(exitCode);
throw new BadImageFormatException();