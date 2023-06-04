﻿namespace UET.Commands.List
{
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.UET.Configuration;
    using Redpoint.UET.Configuration.Engine;
    using Redpoint.UET.Configuration.Plugin;
    using Redpoint.UET.Configuration.Project;
    using System;
    using System.Collections.Generic;
    using Microsoft.Extensions.Logging;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;
    using UET.Commands.Build;
    using UET.Commands.EngineSpec;
    using UET.BuildConfig;

    internal class ListCommand
    {
        internal class Options
        {
            public Option<PathSpec> Path;

            public Options()
            {
                Path = new Option<PathSpec>(
                    "--path",
                    description: "The directory path that contains a BuildConfig.json file. If this parameter isn't provided, defaults to the current working directory.",
                    parseArgument: PathSpec.ParsePathSpec,
                    isDefault: true);
                Path.AddAlias("-p");
                Path.Arity = ArgumentArity.ExactlyOne;
            }
        }

        public static Command CreateListCommand()
        {
            var options = new Options();
            var command = new Command("list", "List the distributions in a BuildConfig.json file.");
            command.AddAllOptions(options);
            command.AddCommonHandler<ListCommandInstance>(
                options,
                services =>
                {
                });
            return command;
        }

        private class ListCommandInstance : ICommandInstance
        {
            private readonly ILogger<ListCommandInstance> _logger;
            private readonly Options _options;

            public ListCommandInstance(
                ILogger<ListCommandInstance> logger,
                Options options)
            {
                _logger = logger;
                _options = options;
            }

            public Task<int> ExecuteAsync(InvocationContext context)
            {
                var path = context.ParseResult.GetValueForOption(_options.Path)!;

                if (path.Type != PathSpecType.BuildConfig)
                {
                    _logger.LogError("You can only list distributions for a BuildConfig.json file.");
                    return Task.FromResult(1);
                }

                var loadResult = BuildConfigLoader.TryLoad(
                    System.IO.Path.Combine(path.DirectoryPath, "BuildConfig.json"));
                if (loadResult.Success)
                {
                    switch (loadResult.BuildConfig!)
                    {
                        case BuildConfigProject buildConfigProject:
                            {
                                _logger.LogInformation($"{buildConfigProject.Distributions.Count} project distribution{(buildConfigProject.Distributions.Count == 1 ? "" : "s")} found{(buildConfigProject.Distributions.Count == 0 ? "." : ":")}");
                                foreach (var distribution in buildConfigProject.Distributions)
                                {
                                    _logger.LogInformation(distribution.Name);
                                }
                            }
                            break;
                        case BuildConfigPlugin buildConfigPlugin:
                            {
                                _logger.LogInformation($"{buildConfigPlugin.Distributions} plugin distribution{(buildConfigPlugin.Distributions.Count == 1 ? "" : "s")} found{(buildConfigPlugin.Distributions.Count == 0 ? "." : ":")}");
                                foreach (var distribution in buildConfigPlugin.Distributions)
                                {
                                    _logger.LogInformation(distribution.Name);
                                }
                            }
                            break;
                        case BuildConfigEngine buildConfigEngine:
                            {
                                _logger.LogInformation($"{buildConfigEngine.Distributions} engine distribution{(buildConfigEngine.Distributions.Count == 1 ? "" : "s")} found{(buildConfigEngine.Distributions.Count == 0 ? "." : ":")}");
                                foreach (var distribution in buildConfigEngine.Distributions)
                                {
                                    _logger.LogInformation(distribution.Name);
                                }
                            }
                            break;
                    }

                    return Task.FromResult(0);
                }

                foreach (var line in loadResult.ErrorList)
                {
                    _logger.LogError(line);
                }
                return Task.FromResult(1);
            }
        }
    }
}