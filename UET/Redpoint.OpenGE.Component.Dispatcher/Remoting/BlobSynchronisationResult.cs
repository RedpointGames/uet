namespace Redpoint.OpenGE.Component.Dispatcher.Remoting
{
    using Redpoint.OpenGE.Protocol;

    internal class BlobSynchronisationResult
    {
        public required long ElapsedUtcTicksHashingInputFiles;
        public required long ElapsedUtcTicksQueryingMissingBlobs;
        public required long ElapsedUtcTicksTransferringCompressedBlobs;
        public required long CompressedDataTransferLength;
    }

    internal class BlobSynchronisationResult<T> : BlobSynchronisationResult
    {
        public required InputFilesByBlobXxHash64 Result;
    }
}
