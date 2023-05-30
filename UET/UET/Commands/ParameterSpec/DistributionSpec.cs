namespace UET.Commands.EngineSpec
{
    using Redpoint.UET.Configuration;
    using Redpoint.UET.Configuration.Engine;
    using Redpoint.UET.Configuration.Plugin;
    using Redpoint.UET.Configuration.Project;
    using System;
    using System.CommandLine;
    using System.CommandLine.Parsing;
    using System.Text.Json;

    internal class DistributionSpec
    {
        private DistributionSpec()
        {
        }

        public static ParseArgument<DistributionSpec?> ParseDistributionSpec(Option<PathSpec> pathSpec)
        {
            return (result) =>
            {
                var path = result.GetValueForOption(pathSpec);
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
                    try
                    {
                        using (var buildConfigStream = new FileStream(
                            System.IO.Path.Combine(path.DirectoryPath, "BuildConfig.json"),
                            FileMode.Open, FileAccess.Read, FileShare.Read))
                        {
                            var buildConfig = JsonSerializer.Deserialize<BuildConfig>(
                                buildConfigStream,
                                BuildConfigSourceGenerationContext.WithDynamicBuildConfig(path.DirectoryPath).BuildConfig);
                            if (buildConfig == null)
                            {
                                result.ErrorMessage = $"The BuildConfig.json file is invalid.";
                                return null!;
                            }

                            switch (buildConfig)
                            {
                                case BuildConfigProject buildConfigProject:
                                    {
                                        var selectedDistributionEntry = buildConfigProject.Distributions
                                            .FirstOrDefault(x => x.Name.Equals(distribution, StringComparison.CurrentCultureIgnoreCase));
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
                                        };
                                    }
                                case BuildConfigPlugin buildConfigPlugin:
                                    {
                                        var selectedDistributionEntry = buildConfigPlugin.Distributions
                                            .FirstOrDefault(x => x.Name.Equals(distribution, StringComparison.CurrentCultureIgnoreCase));
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
                                        };
                                    }
                                case BuildConfigEngine buildConfigEngine:
                                    {
                                        var selectedDistributionEntry = buildConfigEngine.Distributions
                                            .FirstOrDefault(x => x.Name.Equals(distribution, StringComparison.CurrentCultureIgnoreCase));
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
                                        };
                                    }
                            }

                            result.ErrorMessage = $"The BuildConfig.json file is invalid.";
                            return null;
                        }
                    }
                    catch (JsonException ex)
                    {
                        if (ex.LineNumber == 0)
                        {
                            result.ErrorMessage = $"The BuildConfig.json file could not be parsed due to a JSON error: {ex.Message}";
                            return null;
                        }
                        else
                        {
                            var errorLines = new List<string>
                            {
                                $"The BuildConfig.json file could not be parsed due to a JSON error on line {ex.LineNumber}:"
                            };

                            using (var buildConfigStream = new FileStream(
                                System.IO.Path.Combine(path.DirectoryPath, "BuildConfig.json"),
                                FileMode.Open, FileAccess.Read, FileShare.Read))
                            {
                                using (var reader = new StreamReader(buildConfigStream, leaveOpen: true))
                                {
                                    var lineNumber = 1;
                                    while (!reader.EndOfStream)
                                    {
                                        var line = reader.ReadLine();

                                        if (lineNumber > ex.LineNumber - 5 &&
                                            lineNumber <= ex.LineNumber)
                                        {
                                            errorLines.Add($"{lineNumber,5}: {line}");
                                        }
                                        if (lineNumber == ex.LineNumber)
                                        {
                                            errorLines.Add("       " + "↑".PadLeft((int)ex.BytePositionInLine!.Value, ' '));
                                            errorLines.Add("      ┌" + "┘".PadLeft((int)ex.BytePositionInLine!.Value, '─'));
                                            errorLines.Add("      └ " + ex.Message);
                                        }

                                        lineNumber++;
                                    }
                                }
                            }

                            result.ErrorMessage = string.Join("\n", errorLines);
                            return null;
                        }
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

        public override string ToString()
        {
            return DistributionCanonicalName;
        }
    }
}
