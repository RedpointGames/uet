namespace Redpoint.CloudFramework.Storage
{
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;

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
