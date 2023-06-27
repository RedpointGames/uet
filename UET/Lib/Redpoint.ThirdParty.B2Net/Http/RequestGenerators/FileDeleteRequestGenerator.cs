namespace B2Net.Http
{
    using B2Net.Http.RequestGenerators;
    using B2Net.Models;
    using System.Net.Http;
    using System.Text.Json;

    public static class FileDeleteRequestGenerator
    {
        private static class Endpoints
        {
            public const string Delete = "b2_delete_file_version";
        }

        public class FileDeleteRequest
        {
            public string fileId { get; set; }
            public string fileName { get; set; }
        }

        public static HttpRequestMessage Delete(B2Options options, string fileId, string fileName)
        {
            var json = JsonSerializer.Serialize(new FileDeleteRequest { fileId = fileId, fileName = fileName }, B2JsonSerializerContext.Default.FileDeleteRequest);
            return BaseRequestGenerator.PostRequest(Endpoints.Delete, json, options);
        }
    }
}
