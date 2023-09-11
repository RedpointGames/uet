namespace UET.Commands.EngineSpec
{
    using Redpoint.Uet.Configuration.Engine;
    using Redpoint.Uet.Configuration.Plugin;
    using Redpoint.Uet.Configuration.Project;
    using System;
    using System.CommandLine;
    using System.CommandLine.Parsing;
    using UET.BuildConfig;

    internal sealed class DistributionSpec
    {
        private DistributionSpec()
        {
        }

        public static ParseArgument<DistributionSpec?> ParseDistributionSpec(
            IServiceProvider serviceProvider,
            Option<PathSpec> pathSpec)
        {
            return (result) =>
            {
                PathSpec? path;
                try
                {
                    path = result.GetValueForOption(pathSpec);
                }
                catch (InvalidOperationException)
                {
                    // If the path isn't valid, then it will error anyway and there's no point
                    // reporting an error about the distribution.
                    return null;
                }
                if (path == null)
                {
                    // If the path isn't valid, then it will error anyway and there's no point
                    // reporting an error about the distribution.
                    return null;
                }

                if (path.Type == PathSpecType.BuildConfig)
                {
                    if (result.Tokens.Count == 0)
                    {
                        result.ErrorMessage = $"--{result.Argument.Name} must be provided when building against a BuildConfig.json file.";
                        return null;
                    }

                    var distribution = result.Tokens[0].Value;
                    if (string.IsNullOrWhiteSpace(distribution))
                    {
                        result.ErrorMessage = $"--{result.Argument.Name} must not be an empty value.";
                        return null!;
                    }

                    // Check that the distribution is actually defined.
                    var loadResult = BuildConfigLoader.TryLoad(
                        serviceProvider,
                        Path.Combine(path.DirectoryPath, "BuildConfig.json"));
                    if (loadResult.Success)
                    {
                        switch (loadResult.BuildConfig)
                        {
                            case BuildConfigProject buildConfigProject:
                                {
                                    var selectedDistributionEntry = buildConfigProject.Distributions
                                        .FirstOrDefault(x => x.Name.Equals(distribution, StringComparison.OrdinalIgnoreCase));
                                    if (selectedDistributionEntry == null)
                                    {
                                        result.ErrorMessage = $"The distribution '{distribution}' specified by --{result.Argument.Name} was not found in the BuildConfig.json file.";
                                        return null!;
                                    }

                                    return new DistributionSpec
                                    {
                                        DistributionOriginalName = distribution,
                                        DistributionCanonicalName = selectedDistributionEntry.Name,
                                        Distribution = selectedDistributionEntry,
                                        BuildConfig = buildConfigProject,
                                    };
                                }
                            case BuildConfigPlugin buildConfigPlugin:
                                {
                                    var selectedDistributionEntry = buildConfigPlugin.Distributions
                                        .FirstOrDefault(x => x.Name.Equals(distribution, StringComparison.OrdinalIgnoreCase));
                                    if (selectedDistributionEntry == null)
                                    {
                                        result.ErrorMessage = $"The distribution '{distribution}' specified by --{result.Argument.Name} was not found in the BuildConfig.json file.";
                                        return null!;
                                    }

                                    return new DistributionSpec
                                    {
                                        DistributionOriginalName = distribution,
                                        DistributionCanonicalName = selectedDistributionEntry.Name,
                                        Distribution = selectedDistributionEntry,
                                        BuildConfig = buildConfigPlugin,
                                    };
                                }
                            case BuildConfigEngine buildConfigEngine:
                                {
                                    var selectedDistributionEntry = buildConfigEngine.Distributions
                                        .FirstOrDefault(x => x.Name.Equals(distribution, StringComparison.OrdinalIgnoreCase));
                                    if (selectedDistributionEntry == null)
                                    {
                                        result.ErrorMessage = $"The distribution '{distribution}' specified by --{result.Argument.Name} was not found in the BuildConfig.json file.";
                                        return null!;
                                    }

                                    return new DistributionSpec
                                    {
                                        DistributionOriginalName = distribution,
                                        DistributionCanonicalName = selectedDistributionEntry.Name,
                                        Distribution = selectedDistributionEntry,
                                        BuildConfig = buildConfigEngine,
                                    };
                                }
                        }

                        throw new InvalidOperationException("TryLoad returned unexpected BuildConfig object.");
                    }
                    else
                    {
                        result.ErrorMessage = string.Join("\n", loadResult.ErrorList);
                        return null;
                    }
                }
                else
                {
                    if (result.Tokens.Count != 0)
                    {
                        result.ErrorMessage = $"--{result.Argument.Name} must be omitted when not building against a BuildConfig.json file.";
                        return null;
                    }

                    return null;
                }
            };
        }

        /// <summary>
        /// The distribution name, as the user provided it on the command line.
        /// </summary>
        public required string DistributionOriginalName { get; init; }

        /// <summary>
        /// The distribution name, as it is specified in the BuildConfig.json file.
        /// </summary>
        public required string DistributionCanonicalName { get; init; }

        /// <summary>
        /// The distribution object, one of <see cref="BuildConfigProjectDistribution"/>, <see cref="BuildConfigPluginDistribution"/> or <see cref="BuildConfigEngineDistribution"/>.
        /// </summary>
        public required object Distribution { get; init; }

        /// <summary>
        /// The build config object, one of <see cref="BuildConfigProject"/>, <see cref="BuildConfigPlugin"/> or <see cref="BuildConfigEngine"/>.
        /// </summary>
        public required object BuildConfig { get; init; }

        public override string ToString()
        {
            return DistributionCanonicalName;
        }
    }
}
