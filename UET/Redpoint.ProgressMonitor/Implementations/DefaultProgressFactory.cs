namespace Redpoint.ProgressMonitor.Implementations
{
    using System;
    using System.IO;

    internal sealed class DefaultProgressFactory : IProgressFactory
    {
        public IByteBasedProgress CreateProgressForCallback(Func<long> getPosition, Func<long> getLength)
        {
            return new CallbackByteBasedProgress(getPosition, getLength);
        }

        public IByteBasedProgress CreateProgressForStream(Stream stream)
        {
            return new StreamByteBasedProgress(stream);
        }

        public IByteBasedProgress CreateProgressForStream(ChunkedStreamProgress chunkedStreamProgress, long totalLength)
        {
            return new ChunkedStreamByteBasedProgress(chunkedStreamProgress, totalLength);
        }
    }
}
