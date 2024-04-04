namespace Redpoint.OpenGE.Component.Dispatcher.GraphGenerator
{
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.Collections;
    using Redpoint.OpenGE.Component.Dispatcher.Graph;
    using Redpoint.OpenGE.Component.Dispatcher.GraphExecutor;
    using Redpoint.OpenGE.Component.Dispatcher.TaskDescriptorFactories;
    using Redpoint.OpenGE.JobXml;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    internal class DefaultGraphGenerator : IGraphGenerator
    {
        private readonly ITaskDescriptorFactory[] _taskDescriptorFactories;

        public DefaultGraphGenerator(
            IServiceProvider serviceProvider)
        {
            _taskDescriptorFactories = serviceProvider.GetServices<ITaskDescriptorFactory>().ToArray();
        }

        private (ITaskDescriptorFactory? factory, GraphTaskSpec spec) GetFactoryAndArgumentsForTask(
            GraphExecutionEnvironment executionEnvironment,
            JobTask task,
            JobProject project,
            JobEnvironment environment,
            JobTool tool,
            Job job)
        {
            var arguments = CommandLineArgumentSplitter.SplitArguments(tool.Params);
            var spec = new GraphTaskSpec
            {
                ExecutionEnvironment = executionEnvironment,
                Task = task,
                Project = project,
                Environment = environment,
                Tool = tool,
                Job = job,
                Arguments = arguments,
            };

            var currentScore = -1;
            ITaskDescriptorFactory? currentExecutor = null;
            foreach (var executor in _taskDescriptorFactories)
            {
                var score = executor.ScoreTaskSpec(spec);
                if (score != -1 && score > currentScore)
                {
                    currentExecutor = executor;
                    currentScore = score;
                }
            }

            return (currentExecutor, spec);
        }

        public async Task<Graph> GenerateGraphFromJobAsync(
            Job job,
            GraphExecutionEnvironment graphExecutionEnvironment,
            CancellationToken cancellationToken)
        {
            var projects = new Dictionary<string, JobProject>();
            var tasks = new ConcurrentDictionary<string, GraphTask>();
            var dependencies = new DependencyGraph<GraphTask>();
            var immediatelySchedulable = new List<GraphTask>();

            foreach (var projectKv in job.Projects)
            {
                projects[projectKv.Key] = projectKv.Value;

                var deps = new ConcurrentDictionary<string, string[]>();
                await Parallel.ForEachAsync(
                    projectKv.Value.Tasks.ToAsyncEnumerable(),
                    new ParallelOptions
                    {
                        CancellationToken = cancellationToken,
                    },
                    (taskKv, cancellationToken) =>
                    {
                        var (factory, spec) = GetFactoryAndArgumentsForTask(
                            graphExecutionEnvironment,
                            taskKv.Value,
                            projectKv.Value,
                            job.Environments[projectKv.Value.Env],
                            job.Environments[projectKv.Value.Env].Tools[taskKv.Value.Tool],
                            job);
                        if (factory == null)
                        {
                            throw new InvalidOperationException($"No task descriptor factory could handle '{taskKv.Key}', which is almost certainly a bug as the local task descriptor factory should be able to handle everything.");
                        }

                        if (factory.PreparationOperationDescription == null)
                        {
                            // This is a fast describing task, which means we don't
                            // generate a separate step for "preparing" the task.
                            var fastExecutableTask = new FastExecutableGraphTask
                            {
                                GraphTaskSpec = spec,
                                TaskDescriptorFactory = factory,
                            };
                            if (!tasks.TryAdd($"exec:{projectKv.Key}:{taskKv.Key}", fastExecutableTask))
                            {
                                throw new InvalidOperationException($"Conflicting task key 'exec:{projectKv.Key}:{taskKv.Key}'.");
                            }
                            var taskDependencies = (taskKv.Value.DependsOn ?? string.Empty)
                                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                                .Select(x => $"exec:{projectKv.Key}:{x}")
                                .ToArray();
                            if (!deps.TryAdd($"exec:{projectKv.Key}:{taskKv.Key}", taskDependencies))
                            {
                                throw new InvalidOperationException($"Conflicting deps key '{projectKv.Key}:{taskKv.Key}'.");
                            }
                        }
                        else
                        {
                            // This is a slow describing task (relatively), which means
                            // we tell downstream clients when we're doing work to
                            // prepare the descriptor. This encompasses things like
                            // scanning headers locally to figure out remoting dependencies.
                            var describingGraphTask = new DescribingGraphTask
                            {
                                GraphTaskSpec = spec,
                                TaskDescriptorFactory = factory,
                                TaskDescriptor = null,
                            };
                            var executableGraphTask = new ExecutableGraphTask
                            {
                                GraphTaskSpec = spec,
                                DescribingGraphTask = describingGraphTask,
                            };
                            if (!tasks.TryAdd($"desc:{projectKv.Key}:{taskKv.Key}", describingGraphTask))
                            {
                                throw new InvalidOperationException($"Conflicting task key 'desc:{projectKv.Key}:{taskKv.Key}'.");
                            }
                            if (!tasks.TryAdd($"exec:{projectKv.Key}:{taskKv.Key}", executableGraphTask))
                            {
                                throw new InvalidOperationException($"Conflicting task key 'exec:{projectKv.Key}:{taskKv.Key}'.");
                            }
                            var taskDependencies = (taskKv.Value.DependsOn ?? string.Empty)
                                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                                .Select(x => $"exec:{projectKv.Key}:{x}")
                                .ToArray();
                            if (!deps.TryAdd($"desc:{projectKv.Key}:{taskKv.Key}", taskDependencies))
                            {
                                throw new InvalidOperationException($"Conflicting deps key '{projectKv.Key}:{taskKv.Key}'.");
                            }
                            if (!deps.TryAdd($"exec:{projectKv.Key}:{taskKv.Key}", new[] { $"desc:{projectKv.Key}:{taskKv.Key}" }))
                            {
                                throw new InvalidOperationException($"Conflicting deps key '{projectKv.Key}:{taskKv.Key}'.");
                            }
                        }


                        return ValueTask.CompletedTask;
                    }).ConfigureAwait(false);

                foreach (var depKv in deps)
                {
                    var task = tasks[depKv.Key];
                    var taskDependencies = depKv.Value
                        .Where(x => tasks.ContainsKey(x))
                        .Select(x => tasks[x])
                        .ToArray();
                    dependencies.SetDependsOn(task, taskDependencies);
                    if (taskDependencies.Length == 0)
                    {
                        immediatelySchedulable.Add(task);
                    }
                }
            }

            return new Graph
            {
                Projects = projects,
                Tasks = tasks,
                TaskDependencies = dependencies,
                ImmediatelySchedulable = immediatelySchedulable,
            };
        }
    }
}
