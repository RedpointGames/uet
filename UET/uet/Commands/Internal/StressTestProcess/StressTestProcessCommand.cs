namespace UET.Commands.Internal.RunDriveMappedProcess
{
    using Microsoft.Extensions.Logging;
    using Redpoint.CommandLine;
    using Redpoint.ProcessExecution;
    using Redpoint.ProcessExecution.Enumerable;
    using System;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Linq;
    using System.Threading.Tasks;

    internal sealed class StressTestProcessCommand : ICommandDescriptorProvider<UetGlobalCommandContext>
    {
        public static CommandDescriptor<UetGlobalCommandContext> Descriptor => UetCommandDescriptor.NewBuilder()
            .WithOptions<Options>()
            .WithInstance<StressTestProcessCommandInstance>()
            .WithCommand(
                builder =>
                {
                    return new Command("stress-test-process");
                })
            .Build();

        internal sealed class Options
        {
            public Option<string> ProcessPath;
            public Option<string> WorkingDirectory;
            public Option<string[]> Arguments;
            public Option<string[]> ArgumentsAt;
            public Option<int?> TimeoutSeconds;

            public Options()
            {
                ProcessPath = new Option<string>("--process-path");
                WorkingDirectory = new Option<string>("--working-directory");
                Arguments = new Option<string[]>("--arg");
                ArgumentsAt = new Option<string[]>("--arg-at");
                TimeoutSeconds = new Option<int?>("--timeout-seconds");
            }
        }

        private sealed class StressTestProcessCommandInstance : ICommandInstance
        {
            private readonly ILogger<StressTestProcessCommandInstance> _logger;
            private readonly IProcessExecutor _processExecutor;
            private readonly Options _options;

            public StressTestProcessCommandInstance(
                ILogger<StressTestProcessCommandInstance> logger,
                IProcessExecutor processExecutor,
                Options options)
            {
                _logger = logger;
                _processExecutor = processExecutor;
                _options = options;
            }

            public async Task<int> ExecuteAsync(ICommandInvocationContext context)
            {
                var processPath = context.ParseResult.GetValueForOption(_options.ProcessPath) ?? @"C:\Windows\system32\cmd.exe";
                var workingDirectory = context.ParseResult.GetValueForOption(_options.WorkingDirectory);
                var arguments = context.ParseResult.GetValueForOption(_options.Arguments) ?? Array.Empty<string>();
                var argumentsAt = context.ParseResult.GetValueForOption(_options.ArgumentsAt) ?? Array.Empty<string>();
                var timeoutSeconds = context.ParseResult.GetValueForOption(_options.TimeoutSeconds);

                var iteration = 1;
                while (!context.GetCancellationToken().IsCancellationRequested)
                {
                    try
                    {
                        CancellationTokenSource? cts = null;
                        try
                        {
                            if (timeoutSeconds.HasValue)
                            {
                                cts = new CancellationTokenSource(timeoutSeconds.Value * 1000);
                            }
                            using var linkedCts = cts != null
                                ? CancellationTokenSource.CreateLinkedTokenSource(context.GetCancellationToken(), cts.Token)
                                : CancellationTokenSource.CreateLinkedTokenSource(context.GetCancellationToken());

                            _logger.LogInformation($"Iteration #{iteration}: Starting...");
                            var spec = new ProcessSpecification
                            {
                                FilePath = processPath,
                                Arguments = arguments.Concat(argumentsAt.Select(x => '@' + x)).Select(x => new LogicalProcessArgument(x)).ToArray(),
                                WorkingDirectory = workingDirectory,
                            };
                            await foreach (var entry in _processExecutor.ExecuteAsync(spec, linkedCts.Token))
                            {
                                switch (entry)
                                {
                                    case StandardOutputResponse r:
                                        _logger.LogInformation($"Iteration #{iteration}: Standard output: {r.Data}");
                                        break;
                                    case StandardErrorResponse r:
                                        _logger.LogInformation($"Iteration #{iteration}: Standard error: {r.Data}");
                                        break;
                                    case ExitCodeResponse e:
                                        _logger.LogInformation($"Iteration #{iteration}: Exited with code {e.ExitCode}.");
                                        break;
                                }
                            }
                        }
                        finally
                        {
                            cts?.Dispose();
                        }
                        iteration++;
                    }
                    catch (OperationCanceledException) when (context.GetCancellationToken().IsCancellationRequested)
                    {
                        // User requested exit.
                        return 0;
                    }
                    catch (OperationCanceledException)
                    {
                        // User requested exit.
                        _logger.LogError("Command exceeded timeout permitted by --timeout-seconds!");
                        return 1;
                    }
                }

                return 0;
            }
        }
    }
}
