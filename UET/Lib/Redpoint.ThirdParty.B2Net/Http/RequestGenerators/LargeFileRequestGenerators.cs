namespace B2Net.Http.RequestGenerators
{
    using B2Net.Models;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    public class LargeFileRequestGenerators
    {
        private static class Endpoints
        {
            public const string Start = "b2_start_large_file";
            public const string GetPartUrl = "b2_get_upload_part_url";
            public const string Upload = "b2_upload_part";
            public const string Finish = "b2_finish_large_file";
            public const string ListParts = "b2_list_parts";
            public const string Cancel = "b2_cancel_large_file";
            public const string IncompleteFiles = "b2_list_unfinished_large_files";
            public const string CopyPart = "b2_copy_part";
        }

        public static HttpRequestMessage Start(B2Options options, string bucketId, string fileName, string contentType, Dictionary<string, string> fileInfo = null)
        {
            var uri = new Uri(options.ApiUrl + "/b2api/" + Constants.Version + "/" + Endpoints.Start);
            var content = "{\"bucketId\":\"" + bucketId + "\",\"fileName\":\"" + fileName +
                                            "\",\"contentType\":\"" + (string.IsNullOrEmpty(contentType) ? "b2/x-auto" : contentType) + "\"}";
            var request = new HttpRequestMessage()
            {
                Method = HttpMethod.Post,
                RequestUri = uri,
                Content = new StringContent(content),
            };

            request.Headers.TryAddWithoutValidation("Authorization", options.AuthorizationToken);
            // File Info headers
            if (fileInfo != null && fileInfo.Count > 0)
            {
                foreach (var info in fileInfo.Take(10))
                {
                    request.Headers.Add($"X-Bz-Info-{info.Key}", info.Value);
                }
            }
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            request.Content.Headers.ContentLength = content.Length;

            return request;
        }

        /// <summary>
        /// Upload a file to B2. This method will calculate the SHA1 checksum before sending any data.
        /// </summary>
        /// <param name="options"></param>
        /// <param name="fileData"></param>
        /// <param name="partNumber"></param>
        /// <param name="uploadPartUrl"></param>
        /// <returns></returns>
        public static HttpRequestMessage Upload(B2Options options, byte[] fileData, int partNumber, B2UploadPartUrl uploadPartUrl)
        {
            if (partNumber < 1 || partNumber > 10000)
            {
                throw new Exception("Part number must be between 1 and 10,000");
            }

            var uri = new Uri(uploadPartUrl.UploadUrl);
            var request = new HttpRequestMessage()
            {
                Method = HttpMethod.Post,
                RequestUri = uri,
                Content = new ByteArrayContent(fileData)
            };

            // Get the file checksum
            string hash = Utilities.GetSHA1Hash(fileData);

            // Add headers
            request.Headers.TryAddWithoutValidation("Authorization", uploadPartUrl.AuthorizationToken);
            request.Headers.Add("X-Bz-Part-Number", partNumber.ToString());
            request.Headers.Add("X-Bz-Content-Sha1", hash);
            request.Content.Headers.ContentLength = fileData.Length;

            return request;
        }

        public class GetUploadPartUrlRequest
        {
            public string fileId { get; set; }
        }

        public static HttpRequestMessage GetUploadPartUrl(B2Options options, string fileId)
        {
            return BaseRequestGenerator.PostRequest(Endpoints.GetPartUrl, JsonSerializer.Serialize(new GetUploadPartUrlRequest { fileId = fileId }, B2JsonSerializerContext.B2Defaults.GetUploadPartUrlRequest), options);
        }

        public class FinishRequest
        {
            public string fileId { get; set; }
            public string[] partSha1Array { get; set; }
        }

        public static HttpRequestMessage Finish(B2Options options, string fileId, string[] partSHA1Array)
        {
            var content = JsonSerializer.Serialize(new FinishRequest { fileId = fileId, partSha1Array = partSHA1Array }, B2JsonSerializerContext.B2Defaults.FinishRequest);
            var request = BaseRequestGenerator.PostRequestJson(Endpoints.Finish, content, options);
            return request;
        }

        public class ListPartsRequest
        {
            public string fileId { get; set; }
            public int startPartNumber { get; set; }
            public int maxPartCount { get; set; }
        }

        public static HttpRequestMessage ListParts(B2Options options, string fileId, int startPartNumber, int maxPartCount)
        {
            if (startPartNumber < 1 || startPartNumber > 10000)
            {
                throw new Exception("Start part number must be between 1 and 10,000");
            }

            var content = JsonSerializer.Serialize(new ListPartsRequest { fileId = fileId, startPartNumber = startPartNumber, maxPartCount = maxPartCount }, B2JsonSerializerContext.B2Defaults.ListPartsRequest);
            var request = BaseRequestGenerator.PostRequestJson(Endpoints.ListParts, content, options);
            return request;
        }

        public class CancelRequest
        {
            public string fileId { get; set; }
        }

        public static HttpRequestMessage Cancel(B2Options options, string fileId)
        {
            var content = JsonSerializer.Serialize(new CancelRequest { fileId = fileId }, B2JsonSerializerContext.B2Defaults.CancelRequest);
            var request = BaseRequestGenerator.PostRequestJson(Endpoints.Cancel, content, options);
            return request;
        }

        public class IncompleteFilesRequest
        {
            public string bucketId { get; set; }
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public string startFileId { get; set; }
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public string maxFileCount { get; set; }
        }

        public static HttpRequestMessage IncompleteFiles(B2Options options, string bucketId, string startFileId = null, string maxFileCount = null)
        {
            var body = JsonSerializer.Serialize(
                new IncompleteFilesRequest
                {
                    bucketId = bucketId,
                    startFileId = startFileId,
                    maxFileCount = maxFileCount,
                },
                B2JsonSerializerContext.B2Defaults.IncompleteFilesRequest);
            var request = BaseRequestGenerator.PostRequestJson(Endpoints.IncompleteFiles, body, options);
            return request;
        }

        public static HttpRequestMessage CopyPart(B2Options options, string sourceFileId, string destinationLargeFileId, int destinationPartNumber, string range = "")
        {
            var uri = new Uri(options.ApiUrl + "/b2api/" + Constants.Version + "/" + Endpoints.CopyPart);
            var payload = new Dictionary<string, string>() {
                { "sourceFileId", sourceFileId },
                { "largeFileId", destinationLargeFileId },
                { "partNumber", destinationPartNumber.ToString() },
            };
            if (!string.IsNullOrWhiteSpace(range))
            {
                payload.Add("range", range);
            }
            var content = JsonSerializer.Serialize(payload, B2JsonSerializerContext.B2Defaults.DictionaryStringString);
            var request = new HttpRequestMessage()
            {
                Method = HttpMethod.Post,
                RequestUri = uri,
                Content = new StringContent(content),
            };

            request.Headers.TryAddWithoutValidation("Authorization", options.AuthorizationToken);

            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            request.Content.Headers.ContentLength = content.Length;

            return request;
        }
    }
}
