namespace Redpoint.OpenGE.Component.Dispatcher.Remoting
{
    using Microsoft.Extensions.Logging;

    internal static partial class Log
    {
        [LoggerMessage(
            EventId = 0,
            Level = LogLevel.Trace,
            Message = "Remote reports that it is not missing any blobs.")]
        internal static partial void RemoteNotMissingBlobs(ILogger logger);

        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Trace,
            Message = "Remote reports that it is missing {blobCount} blobs.")]
        internal static partial void RemoteMissingBlobs(ILogger logger, int blobCount);
    }
}
