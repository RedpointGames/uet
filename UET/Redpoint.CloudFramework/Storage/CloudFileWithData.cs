namespace Redpoint.CloudFramework.Storage
{
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
}
