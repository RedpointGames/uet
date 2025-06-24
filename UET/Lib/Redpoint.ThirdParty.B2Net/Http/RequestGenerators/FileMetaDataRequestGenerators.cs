namespace B2Net.Http
{
    using B2Net.Http.RequestGenerators;
    using B2Net.Models;
    using System.Net.Http;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    public static class FileMetaDataRequestGenerators
    {
        private static class Endpoints
        {
            public const string List = "b2_list_file_names";
            public const string Versions = "b2_list_file_versions";
            public const string Hide = "b2_hide_file";
            public const string Info = "b2_get_file_info";
        }

        public class GetListRequest
        {
            public string bucketId { get; set; }
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public string startFileName { get; set; }
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public int? maxFileCount { get; set; }
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public string prefix { get; set; }
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public string delimiter { get; set; }
        }

        public static HttpRequestMessage GetList(B2Options options, string bucketId, string startFileName = null, int? maxFileCount = null, string prefix = null, string delimiter = null)
        {
            var body = JsonSerializer.Serialize(
                new GetListRequest
                {
                    bucketId = bucketId,
                    startFileName = string.IsNullOrWhiteSpace(startFileName) ? null : startFileName,
                    maxFileCount = maxFileCount,
                    prefix = string.IsNullOrWhiteSpace(prefix) ? null : prefix,
                    delimiter = string.IsNullOrWhiteSpace(delimiter) ? null : delimiter,
                },
                B2JsonSerializerContext.B2Defaults.GetListRequest);
            return BaseRequestGenerator.PostRequest(Endpoints.List, body, options);
        }

        public class ListVersionsRequest
        {
            public string bucketId { get; set; }
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public string startFileName { get; set; }
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public string startFileId { get; set; }
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public int? maxFileCount { get; set; }
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public string prefix { get; set; }
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public string delimiter { get; set; }
        }

        public static HttpRequestMessage ListVersions(B2Options options, string bucketId, string startFileName = null, string startFileId = null, int? maxFileCount = null, string prefix = null, string delimiter = null)
        {
            var body = JsonSerializer.Serialize(
                new ListVersionsRequest
                {
                    bucketId = bucketId,
                    startFileName = startFileName,
                    startFileId = startFileId,
                    maxFileCount = maxFileCount,
                    prefix = prefix,
                    delimiter = delimiter,
                },
                B2JsonSerializerContext.B2Defaults.ListVersionsRequest);
            return BaseRequestGenerator.PostRequest(Endpoints.Versions, body, options);
        }

        public class HideFileRequest
        {
            public string bucketId { get; set; }
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public string fileName { get; set; }
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public string fileId { get; set; }
        }

        public static HttpRequestMessage HideFile(B2Options options, string bucketId, string fileName = null, string fileId = null)
        {
            var body = JsonSerializer.Serialize(
                new HideFileRequest
                {
                    bucketId = bucketId,
                    fileName = fileName,
                    fileId = fileId,
                },
                B2JsonSerializerContext.B2Defaults.HideFileRequest);
            return BaseRequestGenerator.PostRequest(Endpoints.Hide, body, options);
        }

        public class GetInfoRequest
        {
            public string fileId { get; set; }
        }

        public static HttpRequestMessage GetInfo(B2Options options, string fileId)
        {
            var json = JsonSerializer.Serialize(new GetInfoRequest { fileId = fileId }, B2JsonSerializerContext.B2Defaults.GetInfoRequest);
            return BaseRequestGenerator.PostRequest(Endpoints.Info, json, options);
        }
    }
}
