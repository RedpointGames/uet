namespace Redpoint.ProgressMonitor.Implementations
{
    using System;

    internal sealed class DefaultUtilities : IUtilities
    {
        public string FormatDataAmount(long totalBytes)
        {
            double totalBytesDouble = totalBytes;
            string unit = "b";
            if (totalBytesDouble > 1024)
            {
                totalBytesDouble /= 1024;
                unit = "KB";
            }
            if (totalBytesDouble > 1024)
            {
                totalBytesDouble /= 1024;
                unit = "MB";
            }
            if (totalBytesDouble > 1024)
            {
                totalBytesDouble /= 1024;
                unit = "GB";
            }
            return $"{totalBytesDouble.ToString("#####0.00")} {unit}";
        }

        public string FormatDataTransferRate(double bytesPerSecond)
        {
            string rate = "b/s";
            if (bytesPerSecond > 1024)
            {
                bytesPerSecond /= 1024;
                rate = "KB/s";
            }
            if (bytesPerSecond > 1024)
            {
                bytesPerSecond /= 1024;
                rate = "MB/s";
            }
            if (bytesPerSecond > 1024)
            {
                bytesPerSecond /= 1024;
                rate = "GB/s";
            }
            return $"{bytesPerSecond.ToString("#####0.00")} {rate}";
        }

        public TimeSpan ComputeRemainingTime(DateTimeOffset startTime, DateTimeOffset currentTime, double progress)
        {
            TimeSpan remainingTime = TimeSpan.FromSeconds(100);
            if (progress > 0)
            {
                double bytesRatio = progress / 100.0;
                long msElapsed = currentTime.ToUnixTimeMilliseconds() - startTime.ToUnixTimeMilliseconds();
                //        x                1.0
                //  -------------- = ---------------
                //    ms_elapsed       bytes_ratio
                long msEstimated = (long)((1.0 / bytesRatio) * msElapsed);
                remainingTime = TimeSpan.FromMilliseconds(msEstimated - msElapsed);
            }
            return remainingTime;
        }
    }
}