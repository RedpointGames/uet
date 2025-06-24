namespace Redpoint.CloudFramework.Storage
{
    using B2Net;
    using B2Net.Models;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using System.Web;

    public class B2NetFileStorage : IFileStorage
    {
        private readonly IConfiguration _configuration;

        public B2NetFileStorage(IConfiguration configuration, ILogger<B2NetFileStorage> logger)
        {
            logger.LogInformation("Using Backblaze B2 storage to store files");
            _configuration = configuration;
        }

        // NOTE: You can not cache B2Client instances across requests because they
        // do not refresh their authorization codes and will eventually stop working over time.

        private B2Client GetClient(FileStorageProfile profile)
        {
            if (_configuration.GetSection($"CloudFramework:B2:{profile.Name}") == null)
            {
                throw new InvalidOperationException($"B2 profile {profile.Name} is not configured!");
            }

            B2Options options = new B2Options
            {
                KeyId = _configuration.GetSection($"CloudFramework:B2:{profile.Name}:KeyId")?.Value,
                ApplicationKey = _configuration.GetSection($"CloudFramework:B2:{profile.Name}:ApplicationKey")?.Value,
                BucketId = _configuration.GetSection($"CloudFramework:B2:{profile.Name}:BucketId")?.Value,
                PersistBucket = true,
            };
            return new B2Client(options);
        }

        public async Task<CloudFile> GetInfo(FileStorageProfile profile, string fileId)
        {
            ArgumentNullException.ThrowIfNull(profile);

            var client = GetClient(profile);
            var info = await client.Files.GetInfo(fileId).ConfigureAwait(false);
            return new CloudFile
            {
                FileId = info.FileId,
                Filename = info.FileName,
                Size = info.Size,
            };
        }

        public async Task<CloudFile> Upload(FileStorageProfile profile, byte[] fileData, string fileName)
        {
            ArgumentNullException.ThrowIfNull(profile);

            var b2File = await GetClient(profile).Files.Upload(fileData, fileName).ConfigureAwait(false);
            return new CloudFile
            {
                FileId = b2File.FileId,
                Filename = b2File.FileName,
                Size = b2File.Size,
            };
        }

        public async Task<CloudFile> Upload(FileStorageProfile profile, Stream fileData, string fileName, string contentType)
        {
            ArgumentNullException.ThrowIfNull(profile);

            var client = GetClient(profile);
            var uploadUrl = await client.Files.GetUploadUrl().ConfigureAwait(false);
            var b2File = await client.Files.Upload(fileData, fileName, uploadUrl, contentType, true, dontSHA: true).ConfigureAwait(false);
            return new CloudFile
            {
                FileId = b2File.FileId,
                Filename = b2File.FileName,
                Size = b2File.Size,
            };
        }

        public async Task<CloudFileWithData> Download(FileStorageProfile profile, string fileId)
        {
            ArgumentNullException.ThrowIfNull(profile);

            var client = GetClient(profile);
            var b2Info = await client.Files.GetInfo(fileId).ConfigureAwait(false);
            var b2File = await client.Files.DownloadById(b2Info.FileId).ConfigureAwait(false);

            using var memoryStream = new MemoryStream();
            await b2File.FileData.CopyToAsync(memoryStream).ConfigureAwait(false);

            return new CloudFileWithData(new CloudFile
            {
                FileId = b2Info.FileId,
                Filename = b2Info.FileName,
                Size = b2Info.Size,
            }, memoryStream.ToArray());
        }

        public async Task<CloudFileWithData> Download(FileStorageProfile profile, CloudFile file)
        {
            ArgumentNullException.ThrowIfNull(profile);
            ArgumentNullException.ThrowIfNull(file);

            var b2File = await GetClient(profile).Files.DownloadById(file.FileId).ConfigureAwait(false);

            using var memoryStream = new MemoryStream();
            await b2File.FileData.CopyToAsync(memoryStream).ConfigureAwait(false);

            return new CloudFileWithData(file, memoryStream.ToArray());
        }

        public async Task Delete(FileStorageProfile profile, string fileId)
        {
            ArgumentNullException.ThrowIfNull(profile);

            var client = GetClient(profile);
            var b2Info = await client.Files.GetInfo(fileId).ConfigureAwait(false);
            await client.Files.Delete(b2Info.FileId, b2Info.FileName).ConfigureAwait(false);
        }

        public async Task Delete(FileStorageProfile profile, CloudFile file)
        {
            ArgumentNullException.ThrowIfNull(profile);
            ArgumentNullException.ThrowIfNull(file);

            await GetClient(profile).Files.Delete(file.FileId, file.Filename).ConfigureAwait(false);
        }

        public async Task<string> GetAuthorizedDownloadUrl(FileStorageProfile profile, string fileName, int timeoutInSeconds = 15)
        {
            ArgumentNullException.ThrowIfNull(profile);
            ArgumentNullException.ThrowIfNull(fileName);

            var client = GetClient(profile);
            var authorization = await client.Files.GetDownloadAuthorization(fileName, timeoutInSeconds, _configuration.GetSection($"CloudFramework:B2:{profile.Name}:BucketId").Value).ConfigureAwait(false);
            return _configuration.GetSection($"CloudFramework:B2:{profile.Name}:DownloadPrefix").Value + fileName.Replace("+", "%2B", StringComparison.InvariantCultureIgnoreCase) + "?Authorization=" + HttpUtility.UrlEncode(authorization.AuthorizationToken);
        }

        public async Task<List<CloudFile>> GetList(FileStorageProfile profile, string prefix)
        {
            ArgumentNullException.ThrowIfNull(profile);

            var client = GetClient(profile);

            var files = new List<CloudFile>();
            var startFileName = string.Empty;
            do
            {
                var list = await client.Files.GetListWithPrefixOrDelimiter(startFileName, prefix).ConfigureAwait(false);
                startFileName = list.NextFileName;
                foreach (var file in list.Files)
                {
                    files.Add(new CloudFile
                    {
                        FileId = file.FileId,
                        Filename = file.FileName,
                        Size = file.Size,
                    });
                }
            }
            while (startFileName != null);

            return files;
        }
    }
}
