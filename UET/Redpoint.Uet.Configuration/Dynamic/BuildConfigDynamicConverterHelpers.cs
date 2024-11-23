namespace Redpoint.Uet.Configuration.Dynamic
{
    using System.Text.Json;

    public static class BuildConfigConstants
    {
        public const string Predefined = "Predefined";
    }

    internal static class BuildConfigDynamicConverterHelpers<TDistribution, TBaseClass>
    {
        internal static IDynamicProvider<TDistribution, TBaseClass>? ReadAndGetProvider(
            IDynamicProvider<TDistribution, TBaseClass>[] providers,
            string noun,
            string upperNoun,
            ref Utf8JsonReader readerClone,
            JsonSerializerOptions options)
        {
            if (readerClone.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException($"Expected {noun} entry to be a JSON object.");
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
                    if (propertyName == "Type")
                    {
                        var propertyValue = readerClone.GetString();
                        if (propertyValue == BuildConfigConstants.Predefined)
                        {
                            return null;
                        }
                        if (providers.Length > 0)
                        {
                            foreach (var provider in providers)
                            {
                                if (provider.Type == propertyValue)
                                {
                                    return provider;
                                }
                            }
                            throw new JsonException($"{upperNoun} of type '{propertyValue}' is not recognised as a {noun} provider. Supported {noun} types: {string.Join(", ", providers.Select(x => $"'{x.Type}'"))}");
                        }
                        else
                        {
                            throw new JsonException($"{upperNoun} of type '{propertyValue}' is not recognised as a {noun} provider. There are no supported {noun} types for this type of BuildConfig.json.");
                        }
                    }
                    else
                    {
                        readerClone.TrySkip();
                    }
                }
            }
            if (providers.Length > 0)
            {
                throw new JsonException($"{upperNoun} entry was missing the 'Type' property. It must be set to one of the supported {noun} types: {string.Join(", ", providers.Select(x => $"'{x.Type}'"))}");
            }
            else
            {
                throw new JsonException($"{upperNoun} entry was missing the 'Type' property. There are no supported {noun} types for this type of BuildConfig.json.");
            }
        }

        internal delegate bool ReadIntoDelegate<T>(
            T result,
            ref Utf8JsonReader reader,
            string? propertyName) where T : BuildConfigDynamic<TDistribution, TBaseClass>;

        internal static void ReadInto<T>(
            T result,
            IDynamicProvider<TDistribution, TBaseClass>? provider,
            string noun,
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options,
            ReadIntoDelegate<T> tryReadInto) where T : BuildConfigDynamic<TDistribution, TBaseClass>
        {
            var gotName = false;
            var gotType = false;
            var gotDynamicSettings = false;

            var providerType = provider == null /* Predefined */ ? BuildConfigConstants.Predefined : provider.Type;

            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException($"Expected {noun} entry to be a JSON object.");
            }
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    break;
                }

                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    var propertyName = reader.GetString();
                    reader.Read();
                    switch (propertyName)
                    {
                        case "Name":
                            var name = reader.GetString();
                            if (name == null)
                            {
                                throw new JsonException($"Expected {noun} entry to have a non-null name.");
                            }
                            result.Name = name;
                            gotName = true;
                            break;
                        case "Type":
                            var type = reader.GetString();
                            if (type == null)
                            {
                                throw new JsonException($"Expected {noun} entry to have a non-null type.");
                            }
                            result.Type = type;
                            gotType = true;
                            break;
                        case "Manual":
                            // @todo: Enforce that this can only appear for IDeploymentProvider.
                            result.Manual = reader.GetBoolean();
                            break;
                        default:
                            if (propertyName == providerType)
                            {
                                if (provider == null)
                                {
                                    result.DynamicSettings = reader.GetString() ?? string.Empty;
                                }
                                else
                                {
                                    result.DynamicSettings = provider.DynamicSettings.Deserialize(ref reader);
                                }
                                gotDynamicSettings = true;
                            }
                            else if (!tryReadInto(result, ref reader, propertyName))
                            {
                                // @todo: Make this more accurate for whether 'Manual' can be present.
                                throw new JsonException($"Unexpected property '{propertyName}' found on {noun} entry. Expected only the properties 'Name', 'Type', '{providerType}' and optionally 'Manual' (only for deployments).");
                            }
                            break;
                    }
                }
            }

            if (!gotName)
            {
                throw new JsonException($"Expected property 'Name' to be found on {noun} entry.");
            }
            if (!gotType)
            {
                throw new JsonException($"Expected property 'Type' to be found on {noun} entry.");
            }
            if (!gotDynamicSettings)
            {
                throw new JsonException($"Expected property '{providerType}' to be found on {noun} entry.");
            }
        }

        internal delegate void WriteIntoDelegate<T>(
            T value,
            Utf8JsonWriter writer)
            where T : BuildConfigDynamic<TDistribution, TBaseClass>;

        internal static void WriteInto<T>(
            IDynamicProvider<TDistribution, TBaseClass>[] providers,
            T value,
            Utf8JsonWriter writer,
            JsonSerializerOptions options,
            WriteIntoDelegate<T> writeInto)
            where T : BuildConfigDynamic<TDistribution, TBaseClass>
        {
            writer.WriteStartObject();

            writer.WriteString("Name", value.Name);
            writer.WriteString("Type", value.Type);
            if (value.Manual.HasValue)
            {
                writer.WriteBoolean("Manual", value.Manual.Value);
            }

            writer.WritePropertyName(value.Type);

            if (value.Type == BuildConfigConstants.Predefined)
            {
                writer.WriteStringValue(value.DynamicSettings as string);
            }
            else
            {
                var provider = providers.First(x => x.Type == value.Type);
                provider.DynamicSettings.Serialize(writer, value.DynamicSettings);
            }

            writeInto(value, writer);

            writer.WriteEndObject();
        }
    }
}
