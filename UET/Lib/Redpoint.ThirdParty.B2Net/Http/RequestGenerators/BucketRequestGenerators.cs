namespace B2Net.Http
{
    using B2Net.Http.RequestGenerators;
    using B2Net.Models;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.Json.Serialization.Metadata;
    using System.Text.RegularExpressions;

    public static class BucketRequestGenerators
    {
        private static class Endpoints
        {
            public const string List = "b2_list_buckets";
            public const string Create = "b2_create_bucket";
            public const string Delete = "b2_delete_bucket";
            public const string Update = "b2_update_bucket";
        }

        public class GetBucketListRequest
        {
            public string accountId { get; set; }
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public string bucketName { get; set; }
        }

        public class DeleteBucketRequest
        {
            public string accountId { get; set; }
            public string bucketId { get; set; }
        }

        public class CreateBucketRequest
        {
            public string accountId { get; set; }
            public string bucketName { get; set; }
            public string bucketType { get; set; }
        }

        public class UpdateBucketRequest
        {
            public string accountId { get; set; }
            public string bucketId { get; set; }
            public string bucketType { get; set; }
        }

        public static HttpRequestMessage GetBucketList(B2Options options)
        {
            var json = JsonSerializer.Serialize(new GetBucketListRequest { accountId = options.AccountId, bucketName = null }, B2JsonSerializerContext.B2Defaults.GetBucketListRequest);
            return BaseRequestGenerator.PostRequest(Endpoints.List, json, options);
        }

        public static HttpRequestMessage GetBucketListForName(string bucketName, B2Options options)
        {
            var json = JsonSerializer.Serialize(new GetBucketListRequest { accountId = options.AccountId, bucketName = bucketName }, B2JsonSerializerContext.B2Defaults.GetBucketListRequest);
            return BaseRequestGenerator.PostRequest(Endpoints.List, json, options);
        }

        public static HttpRequestMessage DeleteBucket(B2Options options, string bucketId)
        {
            var json = JsonSerializer.Serialize(new DeleteBucketRequest { accountId = options.AccountId, bucketId = bucketId }, B2JsonSerializerContext.B2Defaults.DeleteBucketRequest);
            return BaseRequestGenerator.PostRequest(Endpoints.Delete, json, options);
        }

        /// <summary>
        /// Create a bucket. Defaults to allPrivate.
        /// </summary>
        /// <param name="options"></param>
        /// <param name="bucketName"></param>
        /// <param name="bucketType"></param>
        /// <returns></returns>
        public static HttpRequestMessage CreateBucket(B2Options options, string bucketName, string bucketType = "allPrivate")
        {
            var allowed = new Regex("^[a-zA-Z0-9-]+$");
            if (bucketName.Length < 6 || bucketName.Length > 50 || !allowed.IsMatch(bucketName) || bucketName.StartsWith("b2-"))
            {
                throw new Exception(@"The bucket name specified does not match the requirements. 
                            Bucket Name can consist of upper-case letters, lower-case letters, numbers, and "" - "", 
                            must be at least 6 characters long, and can be at most 50 characters long");
            }

            var json = JsonSerializer.Serialize(new CreateBucketRequest { accountId = options.AccountId, bucketName = bucketName, bucketType = bucketType }, B2JsonSerializerContext.B2Defaults.CreateBucketRequest);
            return BaseRequestGenerator.PostRequest(Endpoints.Create, json, options);
        }

        /// <summary>
        /// Create a bucket. Defaults to allPrivate.
        /// </summary>
        /// <param name="options"></param>
        /// <param name="bucketName"></param>
        /// <param name="bucketOptions"></param>
        /// <returns></returns>
        public static HttpRequestMessage CreateBucket(B2Options options, string bucketName, B2BucketOptions bucketOptions)
        {
            // Check lifecycle rules
            var hasLifecycleRules = bucketOptions.LifecycleRules != null && bucketOptions.LifecycleRules.Count > 0;
            if (hasLifecycleRules)
            {
                foreach (var rule in bucketOptions.LifecycleRules)
                {
                    if (rule.DaysFromHidingToDeleting < 1 || rule.DaysFromUploadingToHiding < 1)
                    {
                        throw new System.Exception("The smallest number of days you can set in a lifecycle rule is 1.");
                    }
                    if (rule.DaysFromHidingToDeleting == null && rule.DaysFromUploadingToHiding == null)
                    {
                        throw new System.Exception("You must set either DaysFromHidingToDeleting or DaysFromUploadingToHiding. Both cannot be null.");
                    }
                }
            }

            var allowed = new Regex("^[a-zA-Z0-9-]+$");
            if (bucketName.Length < 6 || bucketName.Length > 50 || !allowed.IsMatch(bucketName) || bucketName.StartsWith("b2-"))
            {
                throw new Exception(@"The bucket name specified does not match the requirements. 
                            Bucket Name can consist of upper-case letters, lower-case letters, numbers, and "" - "", 
                            must be at least 6 characters long, and can be at most 50 characters long");
            }

            var body = new B2BucketCreateModel()
            {
                accountId = options.AccountId,
                bucketName = bucketName,
                bucketType = bucketOptions.BucketType.ToString()
            };

            // Add optional options
            if (bucketOptions.CacheControl != 0)
            {
                body.bucketInfo = new Dictionary<string, string>() {
                    { "Cache-Control", "max-age=" + bucketOptions.CacheControl }
                };
            }
            if (hasLifecycleRules)
            {
                body.lifecycleRules = bucketOptions.LifecycleRules;
            }

            // Has cors rules
            if (bucketOptions.CORSRules != null && bucketOptions.CORSRules.Count > 0)
            {
                body.corsRules = bucketOptions.CORSRules;
            }

            var json = JsonSerialize(body, B2JsonSerializerContext.B2Defaults.B2BucketCreateModel);
            return BaseRequestGenerator.PostRequest(Endpoints.Create, json, options);
        }

        /// <summary>
        /// Used to modify the bucket type of the provided bucket.
        /// </summary>
        /// <param name="options"></param>
        /// <param name="bucketId"></param>
        /// <param name="bucketType"></param>
        /// <returns></returns>
        public static HttpRequestMessage UpdateBucket(B2Options options, string bucketId, string bucketType)
        {
            var json = JsonSerializer.Serialize(new UpdateBucketRequest { accountId = options.AccountId, bucketId = bucketId, bucketType = bucketType }, B2JsonSerializerContext.B2Defaults.UpdateBucketRequest);
            return BaseRequestGenerator.PostRequest(Endpoints.Update, json, options);
        }

        /// <summary>
        /// Used to modify the bucket type of the provided bucket.
        /// </summary>
        /// <param name="options"></param>
        /// <param name="bucketId"></param>
        /// <param name="bucketOptions"></param>
        /// <param name="revisionNumber">(optional) When set, the update will only happen if the revision number stored in the B2 service matches the one passed in. This can be used to avoid having simultaneous updates make conflicting changes. </param>
        /// <returns></returns>
        public static HttpRequestMessage UpdateBucket(B2Options options, string bucketId, B2BucketOptions bucketOptions, int? revisionNumber = null)
        {
            // Check lifecycle rules
            var hasLifecycleRules = bucketOptions.LifecycleRules != null && bucketOptions.LifecycleRules.Count > 0;
            if (hasLifecycleRules)
            {
                foreach (var rule in bucketOptions.LifecycleRules)
                {
                    if (rule.DaysFromHidingToDeleting < 1 || rule.DaysFromUploadingToHiding < 1)
                    {
                        throw new System.Exception("The smallest number of days you can set in a lifecycle rule is 1.");
                    }
                    if (rule.DaysFromHidingToDeleting == null && rule.DaysFromUploadingToHiding == null)
                    {
                        throw new System.Exception("You must set either DaysFromHidingToDeleting or DaysFromUploadingToHiding. Both cannot be null.");
                    }
                }
            }

            var body = new B2BucketUpdateModel()
            {
                accountId = options.AccountId,
                bucketId = bucketId,
                bucketType = bucketOptions.BucketType.ToString()
            };

            // Add optional options
            if (bucketOptions.CacheControl != 0)
            {
                body.bucketInfo = new Dictionary<string, string>() {
                    { "Cache-Control", "max-age=" + bucketOptions.CacheControl }
                };
            }
            if (hasLifecycleRules)
            {
                body.lifecycleRules = bucketOptions.LifecycleRules;
            }

            // Has cors rules
            if (bucketOptions.CORSRules != null && bucketOptions.CORSRules.Count > 0)
            {
                if (bucketOptions.CORSRules.Any(x => x.AllowedOperations == null || x.AllowedOperations.Length == 0))
                {
                    throw new System.Exception("You must set allowedOperations on the bucket CORS rules.");
                }
                if (bucketOptions.CORSRules.Any(x => x.AllowedOrigins == null || x.AllowedOrigins.Length == 0))
                {
                    throw new System.Exception("You must set allowedOrigins on the bucket CORS rules.");
                }
                if (bucketOptions.CORSRules.Any(x => string.IsNullOrEmpty(x.CorsRuleName)))
                {
                    throw new System.Exception("You must set corsRuleName on the bucket CORS rules.");
                }
                body.corsRules = bucketOptions.CORSRules;
            }

            if (revisionNumber.HasValue)
            {
                body.ifRevisionIs = revisionNumber.Value;
            }

            var json = JsonSerialize(body, B2JsonSerializerContext.B2Defaults.B2BucketUpdateModel);
            return BaseRequestGenerator.PostRequest(Endpoints.Update, json, options);
        }

        private static string JsonSerialize<T>(T data, JsonTypeInfo<T> typeInfo)
        {
            return JsonSerializer.Serialize<T>(data, typeInfo);
        }
    }

    internal class B2BucketCreateModel
    {
        public string accountId { get; set; }
        public string bucketName { get; set; }
        public string bucketType { get; set; }
        public Dictionary<string, string> bucketInfo { get; set; }
        public List<B2BucketLifecycleRule> lifecycleRules { get; set; }
        public List<B2CORSRule> corsRules { get; set; }
    }

    internal class B2BucketUpdateModel
    {
        public string accountId { get; set; }
        public string bucketId { get; set; }
        public string bucketType { get; set; }
        public Dictionary<string, string> bucketInfo { get; set; }
        public List<B2BucketLifecycleRule> lifecycleRules { get; set; }
        public List<B2CORSRule> corsRules { get; set; }
        public int? ifRevisionIs { get; set; }
    }
}
