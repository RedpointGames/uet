﻿namespace Redpoint.Uet.BuildPipeline.Providers.Test.Plugin.Commandlet
{
    using Microsoft.Extensions.Logging;
    using Redpoint.ProcessExecution;
    using Redpoint.Uet.BuildGraph;
    using Redpoint.Uet.Configuration.Dynamic;
    using Redpoint.Uet.Configuration.Plugin;
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Threading.Tasks;
    using System.Xml;

    internal class CommandletPluginTestProvider : IPluginTestProvider, IDynamicReentrantExecutor<BuildConfigPluginDistribution, BuildConfigPluginTestCommandlet>
    {
        private readonly ILogger<CommandletPluginTestProvider> _logger;
        private readonly IScriptExecutor _scriptExecutor;
        private readonly IProcessExecutor _processExecutor;
        private readonly IPluginTestProjectEmitProvider _pluginTestProjectEmitProvider;

        public CommandletPluginTestProvider(
            ILogger<CommandletPluginTestProvider> logger,
            IScriptExecutor scriptExecutor,
            IProcessExecutor processExecutor,
            IPluginTestProjectEmitProvider pluginTestProjectEmitProvider)
        {
            _logger = logger;
            _scriptExecutor = scriptExecutor;
            _processExecutor = processExecutor;
            _pluginTestProjectEmitProvider = pluginTestProjectEmitProvider;
        }

        public string Type => "Commandlet";

        public object DeserializeDynamicSettings(ref Utf8JsonReader reader, JsonSerializerOptions options)
        {
            return JsonSerializer.Deserialize(
                ref reader,
                TestProviderSourceGenerationContext.WithStringEnum.BuildConfigPluginTestCommandlet)!;
        }

        public void SerializeDynamicSettings(Utf8JsonWriter writer, BuildConfigPluginTestCommandlet value, JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(
                writer,
                value,
                TestProviderSourceGenerationContext.WithStringEnum.BuildConfigPluginTestCommandlet);
        }

        public async Task WriteBuildGraphNodesAsync(
            IBuildGraphEmitContext context,
            XmlWriter writer,
            BuildConfigPluginDistribution buildConfigDistribution,
            IEnumerable<BuildConfigDynamic<BuildConfigPluginDistribution, ITestProvider>> entries)
        {
            var castedSettings = entries
                .Select(x => (name: x.Name, settings: (BuildConfigPluginTestCommandlet)x.DynamicSettings))
                .ToList();

            // Ensure we have the test project available.
            await _pluginTestProjectEmitProvider.EnsureTestProjectNodesArePresentAsync(
                context,
                writer);

            // Emit the nodes to run each test.
            var allPlatforms = castedSettings.SelectMany(x => x.settings.Platforms).Where(context.CanHostPlatformBeUsed).ToHashSet();
            foreach (var platform in allPlatforms)
            {
                await writer.WriteAgentAsync(
                    new AgentElementProperties
                    {
                        Name = $"Commandlet {platform} Tests",
                        Type = platform.ToString()
                    },
                    async writer =>
                    {
                        foreach (var test in castedSettings)
                        {
                            if (!test.settings.Platforms.Contains(platform))
                            {
                                continue;
                            }

                            var nodeName = $"Commandlet {platform} {test.name}";

                            await writer.WriteNodeAsync(
                                new NodeElementProperties
                                {
                                    Name = nodeName,
                                    Requires = _pluginTestProjectEmitProvider.GetTestProjectTags(platform),
                                    If = $"'$(CanBuildEditor{platform})' == 'true'"
                                },
                                async writer =>
                                {
                                    await writer.WriteDynamicReentrantSpawnAsync<
                                        CommandletPluginTestProvider,
                                        BuildConfigPluginDistribution,
                                        BuildConfigPluginTestCommandlet>(
                                        this,
                                        context,
                                        $"{platform}.{test.name}".Replace(" ", "."),
                                        test.settings,
                                        new Dictionary<string, string>
                                        {
                                            { "EnginePath", "$(EnginePath)" },
                                            { "TestProjectPath", _pluginTestProjectEmitProvider.GetTestProjectUProjectFilePath(platform) },
                                            { "RepositoryRoot", "$(ProjectRoot)" },
                                        });
                                });
                            await writer.WriteDynamicNodeAppendAsync(
                                new DynamicNodeAppendElementProperties
                                {
                                    NodeName = nodeName,
                                    MustPassForLaterDeployment = true,
                                });
                        }
                    });
            }
        }

        public async Task<int> ExecuteBuildGraphNodeAsync(
            object configUnknown,
            Dictionary<string, string> runtimeValues,
            CancellationToken cancellationToken)
        {
            var config = (BuildConfigPluginTestCommandlet)configUnknown;

            // @note: System.Threading.Mutex class can't be used with asynchronous code.
            Semaphore? mutex = null;
            if (config.GlobalMutexName != null)
            {
                _logger.LogInformation($"Waiting to obtain global mutex '{config.GlobalMutexName}'...");
                mutex = new Semaphore(1, 1, config.GlobalMutexName);
                while (!mutex.WaitOne(1000))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await Task.Yield();
                }
            }

            try
            {
                // Attempt up to the test attempt count.
                for (var attempt = 0; attempt < (config.TestAttemptCount ?? 1); attempt++)
                {
                    var lastAttempt = attempt == (config.TestAttemptCount ?? 1) - 1;
                    int exitCode;

                    // If a pre-start script is set, run it first.
                    if (!string.IsNullOrWhiteSpace(config.PreStartScriptPath))
                    {
                        exitCode = await _scriptExecutor.ExecutePowerShellAsync(
                            new ScriptSpecification
                            {
                                ScriptPath = Path.Combine(runtimeValues["RepositoryRoot"], config.PreStartScriptPath),
                                Arguments = new[]
                                {
                                    "-EnginePath",
                                    runtimeValues["EnginePath"],
                                    "-TestProjectPath",
                                    runtimeValues["TestProjectPath"],
                                },
                            },
                            CaptureSpecification.Passthrough,
                            cancellationToken);
                        if (exitCode != 0)
                        {
                            if (lastAttempt)
                            {
                                _logger.LogError($"Commandlet pre-start script '{config.PreStartScriptPath}' exited with non-zero exit code {exitCode}.");
                                return exitCode;
                            }
                            else
                            {
                                _logger.LogWarning($"Commandlet pre-start script '{config.PreStartScriptPath}' exited with non-zero exit code {exitCode}, retrying test...");
                                continue;
                            }
                        }
                    }

                    // Run the commandlet.
                    var commandletCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    var timerCts = new CancellationTokenSource();
                    if (config.LogStartTimeoutMinutes != null)
                    {
                        _ = Task.Run(async () =>
                        {
                            await Task.Delay(TimeSpan.FromMinutes(config.LogStartTimeoutMinutes.Value), timerCts.Token);
                            _logger.LogWarning("Commandlet is being cancelled because it timed out.");
                            commandletCts.Cancel();
                        }, timerCts.Token);
                    }
                    exitCode = await _processExecutor.ExecuteAsync(
                        new ProcessSpecification
                        {
                            FilePath = OperatingSystem.IsWindows()
                                ? Path.Combine(runtimeValues["EnginePath"], "Engine", "Binaries", "Win64", "UnrealEditor-Cmd.exe")
                                : Path.Combine(runtimeValues["EnginePath"], "Engine", "Binaries", "Mac", "UnrealEditor-Cmd"),
                            Arguments = new[]
                            {
                                runtimeValues["TestProjectPath"],
                                $"-run={config.Name}",
                                "-NullRHI",
                                "-log",
                                "-stdout",
                                "-FullStdOutLogOutput",
                            }.Concat(config.AdditionalArguments ?? Array.Empty<string>()).ToArray(),
                        },
                        CaptureSpecification.CreateFromDelegates(new CaptureSpecificationDelegates
                        {
                            ReceiveStdout = (line) =>
                            {
                                if (!string.IsNullOrWhiteSpace(config.LogStartSignal))
                                {
                                    if (line.Contains(config.LogStartSignal))
                                    {
                                        // The commandlet is running because we got the signal.
                                        _logger.LogInformation("Detected that the commandlet has started based on LogStartSignal!");
                                        timerCts.Cancel();
                                    }
                                }
                                return true;
                            },
                        }),
                        commandletCts.Token);
                    if (exitCode != 0)
                    {
                        if (lastAttempt)
                        {
                            _logger.LogError($"Commandlet exited with non-zero exit code {exitCode}.");
                            return exitCode;
                        }
                        else
                        {
                            _logger.LogWarning($"Commandlet exited with non-zero exit code {exitCode}, retrying test...");
                            continue;
                        }
                    }

                    // If a validation script is set, run it first.
                    if (!string.IsNullOrWhiteSpace(config.ValidationScriptPath))
                    {
                        exitCode = await _scriptExecutor.ExecutePowerShellAsync(
                            new ScriptSpecification
                            {
                                ScriptPath = Path.Combine(runtimeValues["RepositoryRoot"], config.ValidationScriptPath),
                                Arguments = new[]
                                {
                                    "-EnginePath",
                                    runtimeValues["EnginePath"],
                                    "-TestProjectPath",
                                    runtimeValues["TestProjectPath"],
                                },
                            },
                            CaptureSpecification.Passthrough,
                            cancellationToken);
                        if (exitCode != 0)
                        {
                            if (lastAttempt)
                            {
                                _logger.LogError($"Commandlet validation script '{config.ValidationScriptPath}' exited with non-zero exit code {exitCode}.");
                                return exitCode;
                            }
                            else
                            {
                                _logger.LogWarning($"Commandlet validation script '{config.ValidationScriptPath}' exited with non-zero exit code {exitCode}, retrying test...");
                                continue;
                            }
                        }
                    }

                    _logger.LogInformation($"Commandlet testing passed successfully.");
                    return 0;
                }

                _logger.LogCritical($"Commandlet testing attempt loop exited without reaching any expected condition.");
                return 1;
            }
            finally
            {
                mutex?.Release();
            }
        }
    }
}
