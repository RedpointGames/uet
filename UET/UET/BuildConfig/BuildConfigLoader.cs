namespace UET.BuildConfig
{
    using Redpoint.UET.Configuration.Engine;
    using Redpoint.UET.Configuration.Plugin;
    using Redpoint.UET.Configuration.Project;
    using Redpoint.UET.Configuration;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;

    internal static class BuildConfigLoader
    {
        internal static BuildConfigLoadResult TryLoad(string buildConfigPath)
        {
            try
            {
                using (var buildConfigStream = new FileStream(
                    buildConfigPath,
                    FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var buildConfig = JsonSerializer.Deserialize<BuildConfig>(
                        buildConfigStream,
                        BuildConfigSourceGenerationContext.WithDynamicBuildConfig(Path.GetDirectoryName(buildConfigPath)!).BuildConfig);
                    if (buildConfig == null)
                    {
                        return new BuildConfigLoadResult
                        {
                            Success = false,
                            ErrorList = new List<string>
                            {
                                $"The BuildConfig.json file (at {buildConfigPath}) is invalid."
                            },
                            BuildConfig = null,
                        };
                    }

                    return new BuildConfigLoadResult
                    {
                        BuildConfig = buildConfig,
                        ErrorList = new List<string>(),
                        Success = true,
                    };
                }
            }
            catch (JsonException ex)
            {
                var filePath = buildConfigPath;
                if (ex is IncludedJsonException incex)
                {
                    filePath = incex.FilePath;
                }

                if (ex.LineNumber == 0)
                {
                    return new BuildConfigLoadResult
                    {
                        Success = false,
                        ErrorList = new List<string>
                        {
                            $"The BuildConfig.json file (at {filePath}) could not be parsed due to a JSON error: {ex.Message}",
                        },
                        BuildConfig = null,
                    };
                }
                else
                {
                    var errorLines = new List<string>
                    {
                        $"The BuildConfig.json file (at {filePath}) could not be parsed due to a JSON error on line {ex.LineNumber}:"
                    };

                    using (var buildConfigStream = new FileStream(
                        filePath,
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

                    return new BuildConfigLoadResult
                    {
                        Success = false,
                        ErrorList = errorLines,
                        BuildConfig = null,
                    };
                }
            }
        }
    }
}
