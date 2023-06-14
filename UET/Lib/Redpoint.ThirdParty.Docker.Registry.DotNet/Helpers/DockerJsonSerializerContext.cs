namespace Docker.Registry.DotNet.Helpers
{
    using Docker.Registry.DotNet.Endpoints.Implementations;
    using Docker.Registry.DotNet.Models;
    using Docker.Registry.DotNet.OAuth;
    using System.Net.Http.Json;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    [JsonSerializable(typeof(ImageManifest2_1))]
    [JsonSerializable(typeof(ImageManifest2_2))]
    [JsonSerializable(typeof(ManifestList))]
    [JsonSerializable(typeof(ManifestOperations.SchemaCheck))]
    [JsonSerializable(typeof(Catalog))]
    [JsonSerializable(typeof(ListImageTagsResponse))]
    [JsonSerializable(typeof(OAuthToken))]
    internal partial class DockerJsonSerializerContext : JsonSerializerContext
    {
        public static DockerJsonSerializerContext WithSettings
        {
            get
            {
                var settings = new JsonSerializerOptions();
                settings.Converters.Add(new JsonStringEnumConverter());
                return new DockerJsonSerializerContext(settings);
            }
        }
    }
}