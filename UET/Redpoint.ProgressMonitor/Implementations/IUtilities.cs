namespace Redpoint.ProgressMonitor.Implementations
{
    using System;

    internal interface IUtilities
    {
        string FormatDataAmount(long totalBytes);

        string FormatDataTransferRate(double bytesPerSecond);

        TimeSpan ComputeRemainingTime(DateTimeOffset startTime, DateTimeOffset currentTime, double progress);
    }
}