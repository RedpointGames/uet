namespace UET.Commands.Internal.DynamicReentrantTask
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.Uet.Configuration.Dynamic;
    using Redpoint.Uet.Configuration.Plugin;
    using Redpoint.Uet.Configuration.Project;
    using System;
    using System.Collections.Generic;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Linq;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading.Tasks;

    internal class RunDynamicReentrantTaskCommand
    {
        internal class Options
        {
            public Option<string> DistributionType;
            public Option<string> ReentrantExecutorCategory;
            public Option<string> ReentrantExecutor;
            public Option<FileInfo> TaskJsonPath;
            public Option<string[]> RuntimeSettings;

            public Options()
            {
                DistributionType = new Option<string>("--distribution-type") { IsRequired = true };
                ReentrantExecutorCategory = new Option<string>("--reentrant-executor-category") { IsRequired = true };
                ReentrantExecutorCategory.FromAmong("prepare", "test", "deployment");
                ReentrantExecutor = new Option<string>("--reentrant-executor") { IsRequired = true };
                TaskJsonPath = new Option<FileInfo>("--task-json-path") { IsRequired = true };
                RuntimeSettings = new Option<string[]>("--runtime-setting");
            }
        }

        public static Command CreateRunDynamicReentrantTaskCommand()
        {
            var options = new Options();
            var command = new Command("run-dynamic-reentrant-task");
            command.AddAllOptions(options);
            command.AddCommonHandler<RunDynamicReentrantTaskCommandInstance>(options);
            return command;
        }

        private class RunDynamicReentrantTaskCommandInstance : ICommandInstance
        {
            private readonly ILogger<RunDynamicReentrantTaskCommandInstance> _logger;
            private readonly IServiceProvider _serviceProvider;
            private readonly Options _options;

            public RunDynamicReentrantTaskCommandInstance(
                ILogger<RunDynamicReentrantTaskCommandInstance> logger,
                IServiceProvider serviceProvider,
                Options options)
            {
                _logger = logger;
                _serviceProvider = serviceProvider;
                _options = options;
            }

            public Task<int> ExecuteAsync(InvocationContext context)
            {
                var distributionType = context.ParseResult.GetValueForOption(_options.DistributionType)!;
                var reentrantExecutorCategory = context.ParseResult.GetValueForOption(_options.ReentrantExecutorCategory)!;
                var reentrantExecutorName = context.ParseResult.GetValueForOption(_options.ReentrantExecutor)!;
                var taskJsonPath = context.ParseResult.GetValueForOption(_options.TaskJsonPath)!;
                var runtimeSettings = context.ParseResult.GetValueForOption(_options.RuntimeSettings) ?? Array.Empty<string>();

                if (distributionType == "plugin")
                {
                    return ExecuteAsync<BuildConfigPluginDistribution>(
                        reentrantExecutorCategory,
                        reentrantExecutorName,
                        taskJsonPath,
                        runtimeSettings,
                        context.GetCancellationToken());
                }
                else if (distributionType == "project")
                {
                    return ExecuteAsync<BuildConfigProjectDistribution>(
                        reentrantExecutorCategory,
                        reentrantExecutorName,
                        taskJsonPath,
                        runtimeSettings,
                        context.GetCancellationToken());
                }
                else
                {
                    throw new NotSupportedException();
                }
            }

            private object DeserializeDynamicSettings<T>(IDynamicReentrantExecutor<T> reentrantExecutor, byte[] jsonBytes)
            {
                var reader = new Utf8JsonReader(jsonBytes);
                return reentrantExecutor.DynamicSettings.Deserialize(ref reader);
            }

            private async Task<int> ExecuteAsync<T>(
                string reentrantExecutorCategory,
                string reentrantExecutorName,
                FileInfo taskJsonPath,
                string[] runtimeSettings,
                CancellationToken cancellationToken)
            {
                var reentrantExecutors =
                    _serviceProvider.GetServices<IDynamicReentrantExecutor<T>>()
                    .Where(x => x.Type == reentrantExecutorName)
                    .Where(x => x switch
                    {
                        IPrepareProvider => reentrantExecutorCategory == "prepare",
                        ITestProvider => reentrantExecutorCategory == "test",
                        IDeploymentProvider => reentrantExecutorCategory == "deployment",
                        _ => throw new InvalidOperationException($"Unsupported executor type on {x.GetType().FullName}"),
                    })
                    .ToArray();
                if (reentrantExecutors.Length == 0)
                {
                    _logger.LogCritical($"There is no reentrant executor registered for name '{reentrantExecutorName}'!");
                    return 1;
                }
                else if (reentrantExecutors.Length > 1)
                {
                    _logger.LogCritical($"There is more than one reentrant executor registered for name '{reentrantExecutorName}': {string.Join(", ", reentrantExecutors.Select(x => x.GetType().FullName))}");
                    return 1;
                }
                var reentrantExecutor = reentrantExecutors[0];

                var jsonBytes = await File.ReadAllBytesAsync(taskJsonPath.FullName);
                var config = DeserializeDynamicSettings(reentrantExecutor, jsonBytes);

                var runtimeSettingsDictionary = new Dictionary<string, string>();
                foreach (var setting in runtimeSettings)
                {
                    var kv = setting.Split('=', 2);
                    runtimeSettingsDictionary[kv[0]] = kv[1];
                }

                return await reentrantExecutor.ExecuteBuildGraphNodeAsync(
                    config,
                    runtimeSettingsDictionary,
                    cancellationToken);
            }
        }
    }
}
