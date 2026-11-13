#pragma warning disable APL0002

namespace Redpoint.Vfs.Driver.FsKit
{
    using FSKit;

    public sealed class VirtualFileSystem : FSUnaryFileSystem, IFSUnaryFileSystemOperations
    {
        void IFSUnaryFileSystemOperations.ProbeResource(FSResource resource, FSUnaryFileSystemOperationsProbeResourceResult replyHandler)
        {
            replyHandler(
                FSProbeResult.CreateUsableProbeResult(
                    nameof(VirtualFileSystem),
                    new FSContainerIdentifier { Uuid = Constants.ContainerIdentifier }),
                null);
        }

        void IFSUnaryFileSystemOperations.LoadResource(FSResource resource, FSTaskOptions options, FSUnaryFileSystemOperationsLoadResourceResult replyHandler)
        {
            ContainerStatus = FSContainerStatus.Ready;

            replyHandler(
                new FsKitVolume(resource),
                null);
        }

        void IFSUnaryFileSystemOperations.UnloadResource(FSResource resource, FSTaskOptions options, FSUnaryFileSystemOperationsUnloadResourceResult reply)
        {
            reply(null);
        }

        void IFSUnaryFileSystemOperations.DidFinishLoading()
        {
        }
    }
}
