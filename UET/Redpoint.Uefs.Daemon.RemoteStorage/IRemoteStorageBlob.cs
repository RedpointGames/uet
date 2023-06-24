namespace Redpoint.Uefs.Daemon.RemoteStorage
{
    public interface IRemoteStorageBlob : IDisposable
    {
        long Position { get; set; }

        long Length { get; }

        int Read(byte[] buffer, int offset, int length);

        Task<int> ReadAsync(byte[] buffer, int offset, int length);
    }
}
