namespace Redpoint.KubernetesManager.PxeBoot.Api
{
    using System.Text.Json;
    using System.Text.Json.Serialization;

    [JsonSerializable(typeof(AuthorizeNodeRequest))]
    [JsonSerializable(typeof(AuthorizeNodeResponse))]
    internal partial class ApiJsonSerializerContext : JsonSerializerContext
    {
        public static ApiJsonSerializerContext WithStringEnum = new ApiJsonSerializerContext(new JsonSerializerOptions
        {
            Converters =
            {
                new JsonStringEnumConverter()
            }
        });
    }
}
