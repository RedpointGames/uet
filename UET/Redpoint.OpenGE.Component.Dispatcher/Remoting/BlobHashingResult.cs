namespace Redpoint.OpenGE.Component.Dispatcher.Remoting
{
    using Redpoint.OpenGE.Core;
    using Redpoint.OpenGE.Protocol;

    internal class BlobHashingResult
    {
        public required long ElapsedUtcTicksHashingInputFiles;
        public required InputFilesByBlobXxHash64 Result;
        public required IReadOnlyDictionary<string, long> PathsToContentHashes;
        public required IReadOnlyDictionary<long, BlobInfo> ContentHashesToContent;
    }
}
