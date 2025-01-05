namespace Redpoint.CloudFramework.Storage
{
    using Newtonsoft.Json;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;

    public class CloudFile
    {
        [JsonProperty("fileId")]
        public string FileId { get; set; } = string.Empty;

        [JsonProperty("filename")]
        public string Filename { get; set; } = string.Empty;

        [JsonProperty("size")]
        public long Size { get; internal set; }
    }

    public class CloudFileWithData : CloudFile
    {
        public CloudFileWithData(CloudFile file, byte[] data)
        {
            ArgumentNullException.ThrowIfNull(file);

            FileId = file.FileId;
            Filename = file.Filename;
            FileData = data;
        }

        public ReadOnlyMemory<byte> FileData { get; set; }
    }

    public class FileStorageProfile
    {
        public FileStorageProfile(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public static FileStorageProfile Default { get; } = new FileStorageProfile("Default");
    }

    public interface IFileStorage
    {
        Task<CloudFile> GetInfo(FileStorageProfile profile, string fileId);

        Task<CloudFileWithData> Download(FileStorageProfile profile, string fileId);

        Task<CloudFileWithData> Download(FileStorageProfile profile, CloudFile file);

        Task<CloudFile> Upload(FileStorageProfile profile, byte[] fileData, string fileName);

        Task<CloudFile> Upload(FileStorageProfile profile, Stream fileData, string fileName, string contentType);

        Task Delete(FileStorageProfile profile, string fileId);

        Task Delete(FileStorageProfile profile, CloudFile file);

        Task<string> GetAuthorizedDownloadUrl(FileStorageProfile profile, string fileName, int timeoutInSeconds = 15);

        Task<List<CloudFile>> GetList(FileStorageProfile profile, string prefix);
    }
}
