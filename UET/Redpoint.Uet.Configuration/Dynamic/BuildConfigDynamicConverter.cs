namespace Redpoint.Uet.Configuration.Dynamic
{
    using Microsoft.Extensions.DependencyInjection;
    using System.Text.Json.Serialization;
    using System.Text.Json;

    public abstract class BuildConfigDynamicConverter<TDistribution, TBaseClass> : JsonConverter<BuildConfigDynamic<TDistribution, TBaseClass>>
    {
        private readonly IDynamicProvider<TDistribution, TBaseClass>[] _providers;

        protected abstract string Noun { get; }

        private string UpperNoun => Noun.Substring(0, 1).ToUpper() + Noun.Substring(1);

        public BuildConfigDynamicConverter(IServiceProvider serviceProvider)
        {
            _providers = serviceProvider.GetServices<IDynamicProvider<TDistribution, TBaseClass>>().ToArray();
        }

        private IDynamicProvider<TDistribution, TBaseClass> ReadAndGetProvider(ref Utf8JsonReader readerClone, JsonSerializerOptions options)
        {
            if (readerClone.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException($"Expected {Noun} entry to be a JSON object.");
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
                        if (_providers.Length > 0)
                        {
                            foreach (var provider in _providers)
                            {
                                if (provider.Type == propertyValue)
                                {
                                    return provider;
                                }
                            }
                            throw new JsonException($"{UpperNoun} of type '{propertyValue}' is not recognised as a {Noun} provider. Supported {Noun} types: {string.Join(", ", _providers.Select(x => $"'{x.Type}'"))}");
                        }
                        else
                        {
                            throw new JsonException($"{UpperNoun} of type '{propertyValue}' is not recognised as a {Noun} provider. There are no supported {Noun} types for this type of BuildConfig.json.");
                        }
                    }
                    else
                    {
                        readerClone.TrySkip();
                    }
                }
            }
            if (_providers.Length > 0)
            {
                throw new JsonException($"{UpperNoun} entry was missing the 'Type' property. It must be set to one of the supported {Noun} types: {string.Join(", ", _providers.Select(x => $"'{x.Type}'"))}");
            }
            else
            {
                throw new JsonException($"{UpperNoun} entry was missing the 'Type' property. There are no supported {Noun} types for this type of BuildConfig.json.");
            }
        }

        public override BuildConfigDynamic<TDistribution, TBaseClass>? Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            IDynamicProvider<TDistribution, TBaseClass> provider;
            {
                var readerClone = reader;
                provider = ReadAndGetProvider(ref readerClone, options);
            }

            var result = new BuildConfigDynamic<TDistribution, TBaseClass>();
            var gotName = false;
            var gotType = false;
            var gotDynamicSettings = false;

            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException($"Expected {Noun} entry to be a JSON object.");
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
                                throw new JsonException($"Expected {Noun} entry to have a non-null name.");
                            }
                            result.Name = name;
                            gotName = true;
                            break;
                        case "Type":
                            var type = reader.GetString();
                            if (type == null)
                            {
                                throw new JsonException($"Expected {Noun} entry to have a non-null type.");
                            }
                            result.Type = type;
                            gotType = true;
                            break;
                        case "Manual":
                            // @todo: Enforce that this can only appear for IDeploymentProvider.
                            result.Manual = reader.GetBoolean();
                            break;
                        default:
                            if (propertyName == provider.Type)
                            {
                                result.DynamicSettings = provider.DeserializeDynamicSettings(ref reader, options);
                                gotDynamicSettings = true;
                            }
                            else
                            {
                                // @todo: Make this more accurate for whether 'Manual' can be present.
                                throw new JsonException($"Unexpected property '{propertyName}' found on {Noun} entry. Expected only the properties 'Name', 'Type', '{provider.Type}' and optionally 'Manual' (only for deployments).");
                            }
                            break;
                    }
                }
            }

            if (!gotName)
            {
                throw new JsonException($"Expected property 'Name' to be found on {Noun} entry.");
            }
            if (!gotType)
            {
                throw new JsonException($"Expected property 'Type' to be found on {Noun} entry.");
            }
            if (!gotDynamicSettings)
            {
                throw new JsonException($"Expected property '{provider.Type}' to be found on {Noun} entry.");
            }

            return result;
        }

        public override void Write(
            Utf8JsonWriter writer,
            BuildConfigDynamic<TDistribution, TBaseClass> value,
            JsonSerializerOptions options)
        {
            throw new NotSupportedException();
        }
    }
}
