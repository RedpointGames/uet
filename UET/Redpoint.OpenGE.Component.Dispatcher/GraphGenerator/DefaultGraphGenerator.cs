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

                        if (!tasks.TryAdd(
                            $"{projectKv.Key}:{taskKv.Key}",
                            new GraphTask
                            {
                                GraphTaskSpec = spec,
                                TaskDescriptorFactory = factory,
                            }))
                        {
                            throw new InvalidOperationException($"Conflicting task key '{projectKv.Key}:{taskKv.Key}'.");
                        }
                        return ValueTask.CompletedTask;
                    });

                foreach (var taskKv in projectKv.Value.Tasks)
                {
                    var task = tasks[$"{projectKv.Key}:{taskKv.Key}"];
                    var taskDependencies = (taskKv.Value.DependsOn ?? string.Empty)
                        .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Where(x => tasks.ContainsKey($"{projectKv.Key}:{x}"))
                        .Select(x => tasks[$"{projectKv.Key}:{x}"])
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
