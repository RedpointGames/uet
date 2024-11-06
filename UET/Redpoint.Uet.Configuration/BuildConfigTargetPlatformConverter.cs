namespace Redpoint.Uet.Configuration
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using System.Text.Json;

    public class BuildConfigTargetPlatformConverter : JsonConverter<BuildConfigTargetPlatform[]>
    {
        public override BuildConfigTargetPlatform[] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartArray)
            {
                throw new JsonException("Expected StartArray token");
            }

            var platforms = new List<BuildConfigTargetPlatform>();

            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                if (reader.TokenType == JsonTokenType.String)
                {
                    platforms.Add(new BuildConfigTargetPlatform
                    {
                        Platform = reader.GetString()!
                    });
                }
                else if (reader.TokenType == JsonTokenType.StartObject)
                {
                    var platform = new BuildConfigTargetPlatform();

                    while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                    {
                        if (reader.TokenType != JsonTokenType.PropertyName)
                        {
                            throw new JsonException("Expected PropertyName token");
                        }

                        var propName = reader.GetString();
                        reader.Read();

                        switch (propName)
                        {
                            case nameof(BuildConfigTargetPlatform.Platform):
                                platform.Platform = reader.GetString()!;
                                break;
                            case nameof(BuildConfigTargetPlatform.CookFlavors):
                                if (reader.TokenType != JsonTokenType.StartArray)
                                {
                                    throw new JsonException("Expected StartArray token");
                                }
                                List<string> cookFlavors = [];
                                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                                {
                                    cookFlavors.Add(reader.GetString()!);
                                }
                                platform.CookFlavors = cookFlavors.ToArray();
                                break;
                        }
                    }

                    platforms.Add(platform);
                }
            }

            return platforms.ToArray();
        }

        public override void Write(Utf8JsonWriter writer, BuildConfigTargetPlatform[] value, JsonSerializerOptions options)
        {
            throw new NotSupportedException();
        }
    }
}
