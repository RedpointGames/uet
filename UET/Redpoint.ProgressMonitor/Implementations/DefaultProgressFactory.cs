namespace Redpoint.ProgressMonitor.Implementations
{
    using System;

    internal class DefaultProgressFactory : IProgressFactory
    {
        public IByteBasedProgress CreateProgressForCallback(Func<long> getPosition, Func<long> getLength)
        {
            return new CallbackByteBasedProgress(getPosition, getLength);
        }

        public IByteBasedProgress CreateProgressForStream(Stream stream)
        {
            return new StreamByteBasedProgress(stream);
        }
    }
}
