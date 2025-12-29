namespace Redpoint.KubernetesManager.PxeBoot.Provisioning.Step
{
    using Redpoint.KubernetesManager.Configuration.Types;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading.Tasks;

    public class RkmNodeProvisionerStepJsonConverter : JsonConverter<RkmNodeProvisionerStep>
    {
        private readonly IProvisioningStep[] _provisioningSteps;

        public RkmNodeProvisionerStepJsonConverter(
            IEnumerable<IProvisioningStep> provisioningSteps)
        {
            _provisioningSteps = provisioningSteps.ToArray();
        }

        public override RkmNodeProvisionerStep? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            IProvisioningStep? provisioningStep;
            {
                var readerClone = reader;
                provisioningStep = ReadAndGetProvisioningStep(
                    _provisioningSteps,
                    ref readerClone,
                    options);
            }

            var result = new RkmNodeProvisionerStep();
            ReadInto(
                result,
                provisioningStep!,
                ref reader,
                typeToConvert,
                options);
            return result;
        }

        public override void Write(Utf8JsonWriter writer, RkmNodeProvisionerStep value, JsonSerializerOptions options)
        {
            ArgumentNullException.ThrowIfNull(writer);
            ArgumentNullException.ThrowIfNull(value);

            writer.WriteStartObject();

            writer.WriteString("type", value.Type);

            if (value.DynamicSettings != null)
            {
                writer.WritePropertyName(value.Type[0].ToString().ToLowerInvariant() + value.Type[1..]);
                var provider = _provisioningSteps.First(x => x.Type == value.Type);
                provider.GetJsonType(options).Serialize(writer, value.DynamicSettings);
            }

            writer.WriteEndObject();
        }

        private static IProvisioningStep? ReadAndGetProvisioningStep(
            IProvisioningStep[] provisioningSteps,
            ref Utf8JsonReader readerClone,
            JsonSerializerOptions options)
        {
            if (readerClone.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException($"Expected provisioning step entry to be a JSON object.");
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
                    if (propertyName == "type")
                    {
                        var propertyValue = readerClone.GetString();
                        if (provisioningSteps.Length > 0)
                        {
                            foreach (var provider in provisioningSteps)
                            {
                                if (string.Equals(provider.Type, propertyValue, StringComparison.OrdinalIgnoreCase))
                                {
                                    return provider;
                                }
                            }
                            throw new JsonException($"Provisioning step of type '{propertyValue}' is not recognised as a provisioning step provider. Supported provisioning step types: {string.Join(", ", provisioningSteps.Select(x => $"'{x.Type}'"))}");
                        }
                        else
                        {
                            throw new JsonException($"Provisioning step of type '{propertyValue}' is not recognised as a provisioning step provider. There are no supported provisioning step types for this type of BuildConfig.json.");
                        }
                    }
                    else
                    {
                        readerClone.TrySkip();
                    }
                }
            }
            if (provisioningSteps.Length > 0)
            {
                throw new JsonException($"Provisioning step entry was missing the 'Type' property. It must be set to one of the supported provisioning step types: {string.Join(", ", provisioningSteps.Select(x => $"'{x.Type}'"))}");
            }
            else
            {
                throw new JsonException($"Provisioning step entry was missing the 'Type' property. There are no supported provisioning step types for this type of BuildConfig.json.");
            }
        }

        internal static void ReadInto(
            RkmNodeProvisionerStep result,
            IProvisioningStep provider,
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            var gotType = false;

            var providerType = provider.Type;

            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException($"Expected provisioning step entry to be a JSON object.");
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
                        case "type":
                            var type = reader.GetString();
                            if (type == null)
                            {
                                throw new JsonException($"Expected provisioning step entry to have a non-null type.");
                            }
                            result.Type = type;
                            gotType = true;
                            break;
                        default:
                            if (string.Equals(propertyName, providerType, StringComparison.OrdinalIgnoreCase))
                            {
                                result.DynamicSettings = provider.GetJsonType(options).Deserialize(ref reader);
                            }
                            break;
                    }
                }
            }

            if (!gotType)
            {
                throw new JsonException($"Expected property 'Type' to be found on provisioning step entry.");
            }
        }
    }
}
