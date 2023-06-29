namespace B2Net
{
    using B2Net.Http;
    using B2Net.Http.RequestGenerators;
    using B2Net.Models;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using static B2Net.Utilities;

    [JsonSerializable(typeof(B2AuthResponse))]
    [JsonSerializable(typeof(B2UploadUrl))]
    [JsonSerializable(typeof(BucketRequestGenerators.GetBucketListRequest))]
    [JsonSerializable(typeof(BucketRequestGenerators.DeleteBucketRequest))]
    [JsonSerializable(typeof(BucketRequestGenerators.CreateBucketRequest))]
    [JsonSerializable(typeof(BucketRequestGenerators.UpdateBucketRequest))]
    [JsonSerializable(typeof(B2BucketCreateModel))]
    [JsonSerializable(typeof(B2BucketUpdateModel))]
    [JsonSerializable(typeof(B2File))]
    [JsonSerializable(typeof(B2UploadPartUrl))]
    [JsonSerializable(typeof(B2UploadPart))]
    [JsonSerializable(typeof(B2LargeFileParts))]
    [JsonSerializable(typeof(B2CancelledFile))]
    [JsonSerializable(typeof(B2IncompleteLargeFiles))]
    [JsonSerializable(typeof(B2Bucket))]
    [JsonSerializable(typeof(B2FileList))]
    [JsonSerializable(typeof(B2DownloadAuthorization))]
    [JsonSerializable(typeof(B2BucketListDeserializeModel))]
    [JsonSerializable(typeof(Dictionary<string, object>))]
    [JsonSerializable(typeof(FileDeleteRequestGenerator.FileDeleteRequest))]
    [JsonSerializable(typeof(FileDownloadRequestGenerators.FileDownloadRequest))]
    [JsonSerializable(typeof(FileDownloadRequestGenerators.GetDownloadAuthorizationRequest))]
    [JsonSerializable(typeof(FileMetaDataRequestGenerators.GetListRequest))]
    [JsonSerializable(typeof(FileMetaDataRequestGenerators.ListVersionsRequest))]
    [JsonSerializable(typeof(FileMetaDataRequestGenerators.HideFileRequest))]
    [JsonSerializable(typeof(FileMetaDataRequestGenerators.GetInfoRequest))]
    [JsonSerializable(typeof(FileUploadRequestGenerators.GetUploadUrlRequest))]
    [JsonSerializable(typeof(LargeFileRequestGenerators.GetUploadPartUrlRequest))]
    [JsonSerializable(typeof(LargeFileRequestGenerators.FinishRequest))]
    [JsonSerializable(typeof(LargeFileRequestGenerators.ListPartsRequest))]
    [JsonSerializable(typeof(LargeFileRequestGenerators.CancelRequest))]
    [JsonSerializable(typeof(LargeFileRequestGenerators.IncompleteFilesRequest))]
    [JsonSerializable(typeof(B2Error))]
    internal partial class B2JsonSerializerContext : JsonSerializerContext
    {
        public static B2JsonSerializerContext B2Defaults { get; } = new B2JsonSerializerContext(new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        });
    }
}
