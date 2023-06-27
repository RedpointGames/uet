namespace B2Net.Http
{
    using System.Net.Http;
    using System.Text.Json;
    using System.Text.Json.Serialization.Metadata;
    using System.Threading.Tasks;

    public static class ResponseParser
    {
        public static async Task<T> ParseResponse<T>(HttpResponseMessage response, JsonTypeInfo<T> typeInfo, string callingApi = "")
        {
            var jsonResponse = await response.Content.ReadAsStringAsync();

            await Utilities.CheckForErrors(response, callingApi);

            var obj = JsonSerializer.Deserialize<T>(jsonResponse, typeInfo);
            return obj;
        }
    }
}
