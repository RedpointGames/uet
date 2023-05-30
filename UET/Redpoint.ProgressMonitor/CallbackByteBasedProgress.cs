namespace Redpoint.ProgressMonitor
{
    using System;

    internal class CallbackByteBasedProgress : IByteBasedProgress
    {
        private readonly Func<long> _getPosition;
        private readonly Func<long> _getLength;

        public CallbackByteBasedProgress(Func<long> getPosition, Func<long> getLength)
        {
            _getPosition = getPosition;
            _getLength = getLength;
        }

        public long Position => _getPosition();

        public long Length => _getLength();
    }
}
