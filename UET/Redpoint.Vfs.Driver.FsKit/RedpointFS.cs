#pragma warning disable APL0002

namespace Redpoint.Vfs.Driver.FsKit
{
    using FSKit;

    public sealed class RedpointFS : FSUnaryFileSystem, IFSUnaryFileSystemOperations
    {
        void IFSUnaryFileSystemOperations.ProbeResource(FSResource resource, FSUnaryFileSystemOperationsProbeResourceResult replyHandler)
        {
            replyHandler(
                FSProbeResult.CreateUsableProbeResult(
                    "RedpointFS",
                    new FSContainerIdentifier { Uuid = Constants.ContainerIdentifier }),
                null);
        }

        void IFSUnaryFileSystemOperations.LoadResource(FSResource resource, FSTaskOptions options, FSUnaryFileSystemOperationsLoadResourceResult replyHandler)
        {
            ContainerStatus = FSContainerStatus.Ready;

            replyHandler(
                new RedpointFSVolume(resource),
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
