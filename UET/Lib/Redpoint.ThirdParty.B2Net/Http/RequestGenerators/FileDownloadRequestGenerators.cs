namespace B2Net.Http
{
    using B2Net.Models;
    using System;
    using System.Net.Http;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    public static class FileDownloadRequestGenerators
    {
        private static class Endpoints
        {
            public const string DownloadById = "b2_download_file_by_id";
            public const string GetDownloadAuthorization = "b2_get_download_authorization";
            public const string DownloadByName = "file";
        }

        public class FileDownloadRequest
        {
            public string fileId { get; set; }
        }

        public static HttpRequestMessage DownloadById(B2Options options, string fileId, string byteRange = "")
        {
            var uri = new Uri(options.DownloadUrl + "/b2api/" + Constants.Version + "/" + Endpoints.DownloadById);

            var json = JsonSerializer.Serialize(new FileDownloadRequest { fileId = fileId }, B2JsonSerializerContext.B2Defaults.FileDownloadRequest);
            var request = new HttpRequestMessage()
            {
                Method = HttpMethod.Post,
                RequestUri = uri,
                Content = new StringContent(json)
            };

            request.Headers.TryAddWithoutValidation("Authorization", options.AuthorizationToken);

            // Add byte range header if we have it
            if (!string.IsNullOrEmpty(byteRange))
            {
                request.Headers.Add("Range", $"bytes={byteRange}");
            }

            return request;
        }

        public static HttpRequestMessage DownloadByName(B2Options options, string bucketName, string fileName, string byteRange = "")
        {
            var uri = new Uri(options.DownloadUrl + "/" + Endpoints.DownloadByName + "/" + bucketName + "/" + fileName.b2UrlEncode());
            var request = new HttpRequestMessage()
            {
                Method = HttpMethod.Get,
                RequestUri = uri
            };

            request.Headers.TryAddWithoutValidation("Authorization", options.AuthorizationToken);

            // Add byte range header if we have it
            if (!string.IsNullOrEmpty(byteRange))
            {
                request.Headers.Add("Range", $"bytes={byteRange}");
            }

            return request;
        }

        public class GetDownloadAuthorizationRequest
        {
            public string bucketId { get; set; }
            public string fileNamePrefix { get; set; }
            public long validDurationInSeconds { get; set; }
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public string b2ContentDisposition { get; set; }
        }

        public static HttpRequestMessage GetDownloadAuthorization(B2Options options, string fileNamePrefix, int validDurationInSeconds, string bucketId, string b2ContentDisposition = null)
        {
            var uri = new Uri(options.ApiUrl + "/b2api/" + Constants.Version + "/" + Endpoints.GetDownloadAuthorization);

            var body = JsonSerializer.Serialize(
                new GetDownloadAuthorizationRequest
                {
                    bucketId = bucketId,
                    fileNamePrefix = fileNamePrefix,
                    validDurationInSeconds = validDurationInSeconds,
                    b2ContentDisposition = string.IsNullOrWhiteSpace(b2ContentDisposition) ? null : b2ContentDisposition,
                },
                B2JsonSerializerContext.B2Defaults.GetDownloadAuthorizationRequest);

            var request = new HttpRequestMessage()
            {
                Method = HttpMethod.Post,
                RequestUri = uri,
                Content = new StringContent(body)
            };

            request.Headers.TryAddWithoutValidation("Authorization", options.AuthorizationToken);

            return request;
        }
    }
}
