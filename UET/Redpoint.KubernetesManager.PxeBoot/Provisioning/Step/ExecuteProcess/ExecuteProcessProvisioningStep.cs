namespace Redpoint.KubernetesManager.PxeBoot.Provisioning.Step.ExecuteProcess
{
    using Microsoft.Extensions.Logging;
    using Redpoint.KubernetesManager.Configuration.Types;
    using Redpoint.KubernetesManager.PxeBoot.ProvisioningStep;
    using Redpoint.KubernetesManager.PxeBoot.Variable;
    using Redpoint.PathResolution;
    using Redpoint.ProcessExecution;
    using Redpoint.RuntimeJson;
    using System;
    using System.Collections;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using YamlDotNet.Core.Tokens;

    internal class ExecuteProcessProvisioningStep : IProvisioningStep<ExecuteProcessProvisioningStepConfig>
    {
        private readonly IProcessExecutor _processExecutor;
        private readonly IPathResolver _pathResolver;
        private readonly IVariableProvider _variableProvider;
        private readonly ILogger<ExecuteProcessProvisioningStep> _logger;

        public ExecuteProcessProvisioningStep(
            IProcessExecutor processExecutor,
            IPathResolver pathResolver,
            IVariableProvider variableProvider,
            ILogger<ExecuteProcessProvisioningStep> logger)
        {
            _processExecutor = processExecutor;
            _pathResolver = pathResolver;
            _variableProvider = variableProvider;
            _logger = logger;
        }

        public string Type => "executeProcess";

        public IRuntimeJson GetJsonType(JsonSerializerOptions options)
        {
            return new ProvisioningStepConfigRuntimeJson(options).ExecuteProcessProvisioningStepConfig;
        }

        public ProvisioningStepFlags Flags => ProvisioningStepFlags.None;

        public Task ExecuteOnServerBeforeAsync(ExecuteProcessProvisioningStepConfig config, RkmNodeStatus nodeStatus, IProvisioningStepServerContext serverContext, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        private async Task<int> ExecuteProcessAsync(
            ExecuteProcessProvisioningStepConfig config,
            IProvisioningStepClientContext context,
            CancellationToken cancellationToken)
        {
            var stepValues = new Dictionary<string, string>();
            string? scriptPath = null;
            if (config.Script != null)
            {
                scriptPath = Path.GetTempFileName() + (OperatingSystem.IsWindows() ? ".ps1" : string.Empty);
                stepValues.Add("scriptPath", scriptPath);
            }

            foreach (var kv in _variableProvider.GetEnvironmentVariables(context, stepValues))
            {
                config.EnvironmentVariables[kv.Key] = kv.Value;
            }

            var envVars = new Dictionary<string, string>();
            if (config.InheritEnvironmentVariables)
            {
                foreach (DictionaryEntry kv in Environment.GetEnvironmentVariables())
                {
                    if (kv.Key is string key &&
                        kv.Value is string value &&
                        !string.IsNullOrWhiteSpace(key) &&
                        !string.IsNullOrWhiteSpace(value))
                    {
                        envVars[key] = value;
                    }
                }
            }
            foreach (var kv in config.EnvironmentVariables)
            {
                envVars[kv.Key] = kv.Value;
            }

            IEnumerable<string> arguments = config.Arguments;
            if (config.Script != null && scriptPath != null)
            {
                await File.WriteAllTextAsync(
                    scriptPath,
                    config.Script.Trim(),
                    cancellationToken);
                arguments = config.Arguments.Select(x => _variableProvider.SubstituteVariables(context, x, stepValues));
                _logger.LogInformation($"Wrote temporary script to '{scriptPath}'.");
            }

            try
            {
                var exitCode = await _processExecutor.ExecuteAsync(
                    new ProcessSpecification
                    {
                        FilePath = config.Search
                            ? await _pathResolver.ResolveBinaryPath(config.Executable)
                            : config.Executable,
                        Arguments = [.. arguments],
                        WorkingDirectory = config.WorkingDirectory,
                        EnvironmentVariables = envVars,
                    },
                    CaptureSpecification.Passthrough,
                    cancellationToken);
                if (exitCode != 0 && !config.IgnoreExitCode)
                {
                    throw new UnableToProvisionSystemException($"Process '{config.Executable}' exited with non-zero exit code {exitCode}.");
                }
                return exitCode;
            }
            finally
            {
                if (scriptPath != null && !OperatingSystem.IsLinux())
                {
                    File.Delete(scriptPath);
                }
            }
        }

        private async Task<int> ExecuteProcessWithLogMonitoringAsync(
            ExecuteProcessProvisioningStepConfig config,
            IProvisioningStepClientContext context,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(config.MonitorLogDirectory))
            {
                return await ExecuteProcessAsync(config, context, cancellationToken);
            }

            Directory.CreateDirectory(config.MonitorLogDirectory);

            using var stopWatching = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var watchers = new ConcurrentDictionary<string, Task>();
            void HandleWatcherEvent(object sender, FileSystemEventArgs e)
            {
                if (watchers!.ContainsKey(e.FullPath))
                {
                    return;
                }

                watchers.TryAdd(
                    e.FullPath,
                    Task.Run(
                        async () =>
                        {
                            try
                            {
                                using (var stream = new FileStream(e.FullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                                {
                                    using (var reader = new StreamReader(stream))
                                    {
                                        while (!reader.EndOfStream)
                                        {
                                            var line = await reader.ReadLineAsync(stopWatching.Token);
                                            if (!string.IsNullOrWhiteSpace(line))
                                            {
                                                line = line.TrimEnd();
                                                Console.WriteLine($"{Path.GetFileName(e.FullPath)}: {line}");
                                            }
                                        }
                                    }
                                }
                            }
                            finally
                            {
                                watchers.TryRemove(e.FullPath, out _);
                            }
                        },
                        stopWatching.Token));
            }

            using var watcher = new FileSystemWatcher(config.MonitorLogDirectory);
            watcher.NotifyFilter =
                NotifyFilters.Attributes |
                NotifyFilters.CreationTime |
                NotifyFilters.LastWrite |
                NotifyFilters.Size;
            watcher.Changed += HandleWatcherEvent;
            watcher.Created += HandleWatcherEvent;
            watcher.Renamed += HandleWatcherEvent;
            watcher.Filter = "*";
            watcher.IncludeSubdirectories = false;
            watcher.EnableRaisingEvents = true;

            try
            {
                return await ExecuteProcessAsync(config, context, cancellationToken);
            }
            finally
            {
                stopWatching.Cancel();
                foreach (var task in watchers.Values.ToList())
                {
                    try
                    {
                        await task;
                    }
                    catch
                    {
                    }
                }
            }
        }

        public async Task ExecuteOnClientAsync(ExecuteProcessProvisioningStepConfig config, IProvisioningStepClientContext context, CancellationToken cancellationToken)
        {
            await ExecuteProcessWithLogMonitoringAsync(
                config,
                context,
                cancellationToken);
        }

        public Task ExecuteOnServerAfterAsync(ExecuteProcessProvisioningStepConfig config, RkmNodeStatus nodeStatus, IProvisioningStepServerContext serverContext, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
