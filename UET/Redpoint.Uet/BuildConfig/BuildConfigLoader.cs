namespace UET.BuildConfig
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.Uet.Configuration;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Nodes;

    [SuppressMessage("Design", "CA1052:Static holder types should be Static or NotInheritable", Justification = "Must be non-static for use with ILogger.")]
    public class BuildConfigLoader
    {
        private static void FixSchemaForBuildConfigPath(
            IServiceProvider serviceProvider,
            string buildConfigPath)
        {
            var logger = serviceProvider.GetService<ILogger<BuildConfigLoader>>();

            var requiresSchemaFix = false;
            try
            {
                JsonNode? rootNode = null;
                using (var buildConfigStream = new FileStream(
                    buildConfigPath,
                    FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    rootNode = JsonNode.Parse(buildConfigStream);
                    if (rootNode == null || rootNode.GetValueKind() != JsonValueKind.Object)
                    {
                        // The root element isn't an object, so we can't handle this.
                        return;
                    }
                    var rootObject = rootNode.AsObject();
                    if (rootObject.TryGetPropertyValue("$schema", out var schemaProperty) &&
                        schemaProperty != null &&
                        schemaProperty.GetValueKind() == JsonValueKind.String &&
                        schemaProperty.GetValue<string>() == "https://raw.githubusercontent.com/RedpointGames/uet-schema/main/root.json")
                    {
                        // The '$schema' property is already set to the correct schema.
                        return;
                    }
                    // The '$schema' property isn't set properly. Attempt to fix it.
                    requiresSchemaFix = true;
                }
                if (requiresSchemaFix)
                {
                    // Open the file in write mode now. We don't do this initially in case the file is OK and multiple processes are trying to access it.
                    logger?.LogInformation("Automatically setting '$schema' property to BuildConfig.json to assist with auto-complete.");
                    string newJsonContent;
                    {
                        // We have to re-create the root object to ensure $schema is first.
                        var newRootObject = new JsonObject
                        {
                            { "$schema", JsonValue.Create("https://raw.githubusercontent.com/RedpointGames/uet-schema/main/root.json") },
                        };
                        foreach (var kv in rootNode.AsObject())
                        {
                            if (kv.Key == "$schema" || kv.Value == null)
                            {
                                continue;
                            }
                            newRootObject.Add(kv.Key, kv.Value!.DeepClone());
                        }

                        // Serialize the new content out to the file.
                        var options = new JsonSerializerOptions
                        {
                            WriteIndented = true,
                        };
                        newJsonContent = newRootObject.ToJsonString(options);
                    }
                    using (var buildConfigStream = new FileStream(
                        buildConfigPath,
                        FileMode.OpenOrCreate, FileAccess.Write, FileShare.None))
                    {
                        using (var writer = new StreamWriter(buildConfigStream, Encoding.UTF8, leaveOpen: true))
                        {
                            writer.Write(newJsonContent);
                        }
                        buildConfigStream.SetLength(buildConfigStream.Position);
                    }
                }
            }
            catch (Exception ex)
            {
                if (requiresSchemaFix)
                {
                    logger?.LogError(ex, $"Failed to automatically add '$schema' property to BuildConfig.json file: {ex}");
                }
                return;
            }
        }

        public static BuildConfigLoadResult TryLoad(
            IServiceProvider serviceProvider,
            string buildConfigPath)
        {
            FixSchemaForBuildConfigPath(serviceProvider, buildConfigPath);

            try
            {
                using (var buildConfigStream = new FileStream(
                    buildConfigPath,
                    FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var buildConfig = JsonSerializer.Deserialize(
                        buildConfigStream,
                        BuildConfigSourceGenerationContext.Create(
                            serviceProvider,
                            Path.GetDirectoryName(buildConfigPath)!).BuildConfig);
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
