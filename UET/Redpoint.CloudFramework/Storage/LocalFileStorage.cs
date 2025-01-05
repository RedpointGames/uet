namespace Redpoint.CloudFramework.Storage
{
    using Microsoft.Extensions.Logging;
    using NodaTime;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;

    public class LocalFileStorage : IFileStorage
    {
        private string _storageDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CloudFrameworkTemp", "Data");
        private string _storageNameFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CloudFrameworkTemp", "Names");

        public LocalFileStorage(ILogger<LocalFileStorage> logger)
        {
            logger.LogInformation("Using local file storage to store files");
        }

        private static int NextRandomInt()
        {
#pragma warning disable CA5394 // Do not use insecure randomness
            return Random.Shared.Next();
#pragma warning restore CA5394 // Do not use insecure randomness
        }

        public Task CreateLocalDirectoryStructure(FileStorageProfile profile)
        {
            ArgumentNullException.ThrowIfNull(profile);

            Directory.CreateDirectory(Path.Combine(_storageDataFolder, profile.Name));
            Directory.CreateDirectory(Path.Combine(_storageNameFolder, profile.Name));
            return Task.CompletedTask;
        }

        public Task Delete(FileStorageProfile profile, string fileId)
        {
            ArgumentNullException.ThrowIfNull(profile);

            File.Delete(Path.Combine(_storageDataFolder, profile.Name, fileId));
            File.Delete(Path.Combine(_storageNameFolder, profile.Name, fileId));
            return Task.CompletedTask;
        }

        public Task Delete(FileStorageProfile profile, CloudFile file)
        {
            ArgumentNullException.ThrowIfNull(profile);
            ArgumentNullException.ThrowIfNull(file);

            File.Delete(Path.Combine(_storageDataFolder, profile.Name, file.FileId));
            File.Delete(Path.Combine(_storageNameFolder, profile.Name, file.FileId));
            return Task.CompletedTask;
        }

        public Task<CloudFileWithData> Download(FileStorageProfile profile, string fileId)
        {
            CreateLocalDirectoryStructure(profile);

            var data = File.ReadAllBytes(Path.Combine(_storageDataFolder, profile.Name, fileId));
            var name = File.ReadAllText(Path.Combine(_storageNameFolder, profile.Name, fileId)).Trim();
            return Task.FromResult(new CloudFileWithData(new CloudFile
            {
                FileId = fileId,
                Filename = name,
            }, data));
        }

        public Task<CloudFileWithData> Download(FileStorageProfile profile, CloudFile file)
        {
            ArgumentNullException.ThrowIfNull(file);

            CreateLocalDirectoryStructure(profile);

            var data = File.ReadAllBytes(Path.Combine(_storageDataFolder, profile.Name, file.FileId));
            return Task.FromResult(new CloudFileWithData(file, data));
        }

        public Task<CloudFile> Upload(FileStorageProfile profile, byte[] fileData, string fileName)
        {
            CreateLocalDirectoryStructure(profile);

            var fileId = NextRandomInt() + "-" + SystemClock.Instance.GetCurrentInstant().ToUnixTimeMilliseconds();
            File.WriteAllBytes(Path.Combine(_storageDataFolder, profile.Name, fileId), fileData);
            File.WriteAllText(Path.Combine(_storageNameFolder, profile.Name, fileId), fileName);
            return Task.FromResult(new CloudFile
            {
                FileId = fileId,
                Filename = fileName,
            });
        }

        public Task<CloudFile> Upload(FileStorageProfile profile, Stream fileData, string fileName, string contentType)
        {
            ArgumentNullException.ThrowIfNull(fileData);

            CreateLocalDirectoryStructure(profile);

            var fileId = NextRandomInt() + "-" + SystemClock.Instance.GetCurrentInstant().ToUnixTimeMilliseconds();
            using (var stream = File.OpenWrite(Path.Combine(_storageDataFolder, profile.Name, fileId)))
            {
                fileData.CopyTo(stream);
                fileData.Flush();
            }
            File.WriteAllText(Path.Combine(_storageNameFolder, profile.Name, fileId), fileName);
            return Task.FromResult(new CloudFile
            {
                FileId = fileId,
                Filename = fileName,
            });
        }

        public Task<string> GetAuthorizedDownloadUrl(FileStorageProfile profile, string fileName, int timeoutInSeconds = 15)
        {
            throw new InvalidOperationException("GetAuthorizedDownloadUrl not supported with LocalFileStorage implementation.");
        }

        public Task<CloudFile> GetInfo(FileStorageProfile profile, string fileId)
        {
            ArgumentNullException.ThrowIfNull(profile);

            var name = File.ReadAllText(Path.Combine(_storageNameFolder, profile.Name, fileId)).Trim();
            return Task.FromResult(new CloudFile
            {
                FileId = fileId,
                Filename = name,
            });
        }

        public Task<List<CloudFile>> GetList(FileStorageProfile profile, string prefix)
        {
            ArgumentNullException.ThrowIfNull(profile);

            var results = new List<CloudFile>();
            foreach (var fileInfo in new DirectoryInfo(Path.Combine(_storageNameFolder, profile.Name)).GetFiles())
            {
                results.Add(new CloudFile
                {
                    FileId = fileInfo.Name,
                    Filename = File.ReadAllText(fileInfo.FullName).Trim(),
                });
            }
            return Task.FromResult(results);
        }
    }
}
