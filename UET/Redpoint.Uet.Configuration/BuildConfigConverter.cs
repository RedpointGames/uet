namespace Redpoint.Uet.Configuration
{
    using Redpoint.Uet.Configuration.Engine;
    using Redpoint.Uet.Configuration.Plugin;
    using Redpoint.Uet.Configuration.Project;
    using System;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    public class BuildConfigConverter : JsonConverter<BuildConfig>
    {
        private readonly string _basePathForIncludes;

        public BuildConfigConverter(string basePathForIncludes)
        {
            _basePathForIncludes = basePathForIncludes;
        }

        private (List<string> includes, BuildConfigType type) ReadBase(ref Utf8JsonReader readerClone, JsonSerializerOptions options)
        {
            var includes = new List<string>();
            BuildConfigType? type = null;
            if (readerClone.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException("Expected BuildConfig.json to contain an object.");
            }
            while (readerClone.Read())
            {
                if (readerClone.TokenType == JsonTokenType.EndObject)
                {
                    break;
                }

                if (readerClone.TokenType == JsonTokenType.PropertyName)
                {
                    var propertyName = readerClone.GetString();
                    readerClone.Read();
                    switch (propertyName)
                    {
                        case "Type":
                            switch (readerClone.GetString()?.ToLowerInvariant())
                            {
                                case "plugin":
                                    type = BuildConfigType.Plugin;
                                    break;
                                case "project":
                                    type = BuildConfigType.Project;
                                    break;
                                case "engine":
                                    type = BuildConfigType.Engine;
                                    break;
                                default:
                                    type = null;
                                    break;
                            }
                            break;
                        case "Include":
                            if (readerClone.TokenType != JsonTokenType.StartArray)
                            {
                                throw new JsonException("Expected 'Include' to be an array");
                            }
                            while (readerClone.Read())
                            {
                                if (readerClone.TokenType == JsonTokenType.EndArray)
                                {
                                    break;
                                }

                                if (readerClone.TokenType == JsonTokenType.String)
                                {
                                    includes.Add(readerClone.GetString()!);
                                }
                                else
                                {
                                    readerClone.TrySkip();
                                }
                            }
                            break;
                        default:
                            readerClone.TrySkip();
                            break;
                    }
                }
            }

            if (type == null)
            {
                throw new JsonException("Missing the 'Type' field, or it doesn't contain a valid value. It should be \"Plugin\", \"Project\" or \"Engine\".");
            }

            return (includes, type.Value);
        }

        public override BuildConfig? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var readerClone = reader;
            var (includes, type) = ReadBase(ref readerClone, options);

            var generationContext = new BuildConfigSourceGenerationContext(new JsonSerializerOptions(options));

            BuildConfig baseConfig = type switch
            {
                BuildConfigType.Plugin => JsonSerializer.Deserialize(ref reader, generationContext.BuildConfigPlugin)!,
                BuildConfigType.Project => JsonSerializer.Deserialize(ref reader, generationContext.BuildConfigProject)!,
                BuildConfigType.Engine => JsonSerializer.Deserialize(ref reader, generationContext.BuildConfigEngine)!,
                _ => throw new JsonException("The BuildConfig does not contain a valid type.")
            };

            foreach (var include in includes)
            {
                var targetPath = Path.Combine(_basePathForIncludes, include, "BuildConfig.json");
                try
                {
                    if (File.Exists(targetPath))
                    {
                        using (var stream = new FileStream(targetPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                        {
                            // @todo: Figure out how to do "Include" recursively.

                            switch (type)
                            {
                                case BuildConfigType.Plugin:
                                    var includedConfigPlugin = JsonSerializer.Deserialize(stream, generationContext.BuildConfigPluginIncludeFragment)!;
                                    ((BuildConfigPlugin)baseConfig).Distributions.AddRange(includedConfigPlugin.Distributions);
                                    break;
                                case BuildConfigType.Project:
                                    var includedConfigProject = JsonSerializer.Deserialize(stream, generationContext.BuildConfigProjectIncludeFragment)!;
                                    var projectDistributions = includedConfigProject.Distributions;
                                    foreach (var distribution in projectDistributions)
                                    {
                                        distribution.FolderName = Path.Combine(include, distribution.FolderName);
                                    }
                                    ((BuildConfigProject)baseConfig).Distributions.AddRange(projectDistributions);
                                    break;
                                case BuildConfigType.Engine:
                                    var includedConfigEngine = JsonSerializer.Deserialize(stream, generationContext.BuildConfigEngineIncludeFragment)!;
                                    ((BuildConfigEngine)baseConfig).Distributions.AddRange(includedConfigEngine.Distributions);
                                    break;
                            }
                        }
                    }
                }
                catch (JsonException ex)
                {
                    throw new IncludedJsonException(ex, targetPath);
                }
            }

            return baseConfig;
        }

        public override void Write(Utf8JsonWriter writer, BuildConfig value, JsonSerializerOptions options)
        {
            throw new NotSupportedException();
        }
    }
}
