namespace B2Net.Http
{
    using B2Net.Models;
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Text.Json;

    public static class FileCopyRequestGenerators
    {
        private static class Endpoints
        {
            public const string Copy = "b2_copy_file";
        }

        public static HttpRequestMessage Copy(B2Options options, string sourceFileId, string fileName, B2MetadataDirective metadataDirective,
            string contentType = "", Dictionary<string, string> fileInfo = null, string range = "", string destinationBucketId = "")
        {
            var uri = new Uri(options.ApiUrl + "/b2api/" + Constants.Version + "/" + Endpoints.Copy);

            var payload = new Dictionary<string, object>() {
                {"sourceFileId", sourceFileId},
                {"fileName", fileName},
                {"metadataDirective", metadataDirective.ToString()},
            };
            if (!string.IsNullOrWhiteSpace(range))
            {
                payload.Add("range", range);
            }
            if (!string.IsNullOrWhiteSpace(destinationBucketId))
            {
                payload.Add("destinationBucketId", destinationBucketId);
            }
            if (metadataDirective == B2MetadataDirective.REPLACE)
            {
                if (string.IsNullOrWhiteSpace(contentType))
                {
                    payload.Add("contentType", "b2/x-auto");
                }
                else
                {
                    payload.Add("contentType", contentType);
                }
            }
            // File Info
            if (metadataDirective == B2MetadataDirective.REPLACE && fileInfo != null && fileInfo.Count > 0)
            {
                payload.Add("fileInfo", fileInfo);
            }
            var json = JsonSerializer.Serialize(payload, B2JsonSerializerContext.Default.DictionaryStringObject);

            var request = new HttpRequestMessage()
            {
                Method = HttpMethod.Post,
                RequestUri = uri,
                Content = new StringContent(json)
            };

            request.Headers.TryAddWithoutValidation("Authorization", options.AuthorizationToken);

            return request;
        }
    }
}
