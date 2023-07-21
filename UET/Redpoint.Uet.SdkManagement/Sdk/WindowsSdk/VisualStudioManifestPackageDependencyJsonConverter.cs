namespace Redpoint.Uet.SdkManagement.AutoSdk.WindowsSdk
{
    using System;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    class VisualStudioManifestPackageDependencyJsonConverter : JsonConverter<VisualStudioManifestPackageDependency>
    {
        public override VisualStudioManifestPackageDependency? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                return new VisualStudioManifestPackageDependency
                {
                    Version = reader.GetString(),
                };
            }
            else if (reader.TokenType == JsonTokenType.StartObject)
            {
                var value = new VisualStudioManifestPackageDependency();
                var when = new List<string>();
                var whenSet = false;
                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.EndObject)
                    {
                        if (whenSet)
                        {
                            value.When = when.ToArray();
                        }
                        return value;
                    }

                    if (reader.TokenType != JsonTokenType.PropertyName)
                    {
                        throw new JsonException();
                    }

                    var propertyName = reader.GetString();

                    reader.Read();

                    if (reader.TokenType == JsonTokenType.String)
                    {
                        switch (propertyName)
                        {
                            case "id":
                                value.Id = reader.GetString();
                                break;
                            case "type":
                                value.Type = reader.GetString();
                                break;
                            case "version":
                                value.Version = reader.GetString();
                                break;
                            case "behaviors":
                                value.Behaviours = reader.GetString();
                                break;
                        }
                    }
                    else if (reader.TokenType == JsonTokenType.StartArray)
                    {
                        switch (propertyName)
                        {
                            case "when":
                                whenSet = true;
                                while (reader.Read())
                                {
                                    if (reader.TokenType == JsonTokenType.String)
                                    {
                                        when.Add(reader.GetString()!);
                                    }
                                    else if (reader.TokenType == JsonTokenType.EndArray)
                                    {
                                        break;
                                    }
                                }
                                break;
                            default:
                                reader.TrySkip();
                                break;
                        }
                    }
                    else
                    {
                        reader.TrySkip();
                    }
                }
            }

            throw new JsonException("Invalid dependency declaration.");
        }

        public override void Write(Utf8JsonWriter writer, VisualStudioManifestPackageDependency value, JsonSerializerOptions options)
        {
            throw new NotSupportedException();
        }
    }
}
