namespace Redpoint.Vfs.Client
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    internal class NullVfsClient : IVfsClient
    {
        public Task MountAsync(string vfsHostName, Uri sourceUri, string mountPath)
        {
            throw new NotSupportedException("This platform does not support virtual filesystems.");
        }

        public Task UnmountAsync(string vfsHostName, string mountPath)
        {
            throw new NotSupportedException("This platform does not support virtual filesystems.");
        }
    }
}
