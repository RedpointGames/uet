namespace B2Net
{
    using B2Net.Models;
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading.Tasks;

    public static class Utilities
    {
        /// <summary>
        /// Create the B2 Authorization header. Base64 encoded accountId:applicationKey.
        /// </summary>
        public static string CreateAuthorizationHeader(string accountId, string applicationKey)
        {
            var authHeader = "Basic ";
            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes(accountId + ":" + applicationKey));
            return authHeader + credentials;
        }

        public static async Task CheckForErrors(HttpResponseMessage response, string callingApi = "")
        {
            if (!response.IsSuccessStatusCode)
            {
                // Should retry
                bool retry = response.StatusCode == (HttpStatusCode)429 ||
                    response.StatusCode == HttpStatusCode.RequestTimeout ||
                    response.StatusCode == HttpStatusCode.ServiceUnavailable;

                string content = await response.Content.ReadAsStringAsync();

                B2Error b2Error;
                try
                {
                    b2Error = JsonSerializer.Deserialize(content, B2JsonSerializerContext.B2Defaults.B2Error);
                }
                catch (Exception ex)
                {
                    throw new Exception("Seralization of the response failed. See inner exception for response contents and serialization error.", ex);
                }
                if (b2Error != null)
                {
                    // If calling API is supplied, append to the error message
                    if (!string.IsNullOrEmpty(callingApi) && b2Error.Status == 401)
                    {
                        b2Error.Message = $"Unauthorized error when operating on {callingApi}. Are you sure the key you are using has access? {b2Error.Message}";
                    }
                    throw new B2Exception(b2Error.Code, b2Error.Status, b2Error.Message, retry);
                }
            }
        }

        public static string GetSHA1Hash(byte[] fileData)
        {
            using (var sha1 = SHA1.Create())
            {
                return HexStringFromBytes(sha1.ComputeHash(fileData));
            }
        }

        public static string DetermineBucketId(B2Options options, string bucketId)
        {
            // Check for a persistant bucket
            if (!options.PersistBucket && string.IsNullOrEmpty(bucketId))
            {
                throw new ArgumentNullException(nameof(bucketId), "You must either Persist a Bucket or provide a BucketId in the method call.");
            }

            // Are we persisting buckets? If so use the one from settings
            return options.PersistBucket ? options.BucketId : bucketId;
        }

        private static string HexStringFromBytes(byte[] bytes)
        {
            var sb = new StringBuilder();
            foreach (byte b in bytes)
            {
                var hex = b.ToString("x2");
                sb.Append(hex);
            }
            return sb.ToString();
        }

        internal class B2Error
        {
            [JsonPropertyName("code")]
            public string Code { get; set; }
            [JsonPropertyName("message")]
            public string Message { get; set; }
            [JsonPropertyName("status")]
            public int Status { get; set; }
        }
    }

    public static class B2StringExtension
    {
        public static string b2UrlEncode(this string str)
        {
            if (str == "/")
            {
                return str;
            }
            // Decode / back to un-encoded value
            return Uri.EscapeDataString(str).Replace("%2F", "/");
        }

        public static string b2UrlDecode(this string str)
        {
            if (str == "+")
            {
                return " ";
            }
            return Uri.UnescapeDataString(str);
        }
    }
}
