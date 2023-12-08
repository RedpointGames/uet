namespace Redpoint.Uefs.Protocol
{
    using Google.Protobuf.WellKnownTypes;
    using System;

#pragma warning disable CS1591

    public static class PollingResponseExtensions
    {
        /// <summary>
        /// Convert the polling status to a displayable string.
        /// </summary>
        /// <param name="status">The status to convert.</param>
        /// <returns>The displayable string.</returns>
        public static string ToDisplayString(this PollingResponseStatus status)
        {
            switch (status)
            {
                case PollingResponseStatus.Unknown:
                    return "unknown";
                case PollingResponseStatus.Waiting:
                    return "waiting";
                case PollingResponseStatus.Starting:
                    return "starting";
                case PollingResponseStatus.Checking:
                    return "checking";
                case PollingResponseStatus.Pulling:
                    return "pulling";
                case PollingResponseStatus.Verifying:
                    return "verifying";
                case PollingResponseStatus.Complete:
                    return "complete";
                case PollingResponseStatus.Error:
                    return "error";
            }
            return "unknown";
        }

        /// <summary>
        /// Move the polling response to an error status.
        /// </summary>
        /// <param name="response">The polling response.</param>
        /// <param name="error">The error message.</param>
        public static void Error(this PollingResponse response, string error)
        {
            ArgumentNullException.ThrowIfNull(response);
            response.Err = error;
            response.Complete = true;
            response.Status = PollingResponseStatus.Error;
        }

        /// <summary>
        /// Move the polling response to an error status with the given exception.
        /// </summary>
        /// <param name="response">The polling response.</param>
        /// <param name="ex">The exception.</param>
        public static void Exception(this PollingResponse response, Exception ex)
        {
            ArgumentNullException.ThrowIfNull(response);
            ArgumentNullException.ThrowIfNull(ex);
            response.Err = ex.ToString();
            response.Complete = true;
            response.Status = PollingResponseStatus.Error;
        }

        public static void Init(this PollingResponse response, PollingResponseType type)
        {
            ArgumentNullException.ThrowIfNull(response);
            response.Type = type;
            response.Complete = false;
            response.Status = PollingResponseStatus.Waiting;
            response.Err = string.Empty;
            response.PackagePath = string.Empty;
            response.PackageHash = string.Empty;
            response.Position = 0;
            response.Length = 0;
        }

        public static void Starting(this PollingResponse response)
        {
            ArgumentNullException.ThrowIfNull(response);
            response.Complete = false;
            response.Status = PollingResponseStatus.Starting;
            response.Err = string.Empty;
            response.PackagePath = string.Empty;
            response.PackageHash = string.Empty;
            response.Position = 0;
            response.Length = 0;
        }

        public static void Checking(this PollingResponse response)
        {
            ArgumentNullException.ThrowIfNull(response);
            response.Complete = false;
            response.Status = PollingResponseStatus.Checking;
            response.Err = string.Empty;
            response.PackagePath = string.Empty;
            response.PackageHash = string.Empty;
            response.Position = 0;
            response.Length = 0;
        }

        public static void VerifyingPackages(this PollingResponse response, int totalPackages)
        {
            ArgumentNullException.ThrowIfNull(response);
            response.Complete = false;
            response.Status = PollingResponseStatus.Verifying;
            response.VerifyPackageTotal = totalPackages;
        }

        public static void VerifyingPackage(this PollingResponse response, int packageIndex)
        {
            ArgumentNullException.ThrowIfNull(response);
            response.VerifyPackageIndex = packageIndex;
        }

        public static void VerifyingChunk(this PollingResponse response, long length)
        {
            ArgumentNullException.ThrowIfNull(response);
            response.Complete = false;
            response.Status = PollingResponseStatus.Verifying;
            response.Length = length;
        }

        public static void VerifyingChunkUpdatePosition(this PollingResponse response, long position)
        {
            ArgumentNullException.ThrowIfNull(response);
            response.Position = position;
        }

        public static void VerifyingChunkIncrementFixed(this PollingResponse response)
        {
            ArgumentNullException.ThrowIfNull(response);
            response.VerifyChunksFixed++;
        }

        public static void CompleteForVerifying(this PollingResponse response)
        {
            ArgumentNullException.ThrowIfNull(response);
            response.Complete = true;
            response.Status = PollingResponseStatus.Complete;
            response.Err = string.Empty;
        }

        public static void CompleteForMount(this PollingResponse response)
        {
            ArgumentNullException.ThrowIfNull(response);
            response.Complete = true;
            response.Status = PollingResponseStatus.Complete;
            response.Err = string.Empty;
        }

        public static void CompleteForGit(this PollingResponse response)
        {
            ArgumentNullException.ThrowIfNull(response);
            response.Complete = true;
            response.Status = PollingResponseStatus.Complete;
            response.Err = string.Empty;
        }

        public static void CompleteForPackage(
            this PollingResponse response,
            string packagePath,
            string packageHash)
        {
            ArgumentNullException.ThrowIfNull(response);
            response.Complete = true;
            response.Status = PollingResponseStatus.Complete;
            response.Err = string.Empty;
            response.PackagePath = packagePath;
            response.PackageHash = packageHash;
        }

        public static void CompleteForPackageWithLength(
            this PollingResponse response,
            string packagePath,
            string packageHash,
            long length)
        {
            ArgumentNullException.ThrowIfNull(response);
            response.Complete = true;
            response.Status = PollingResponseStatus.Complete;
            response.Err = string.Empty;
            response.PackagePath = packagePath;
            response.PackageHash = packageHash;
            response.Position = 0;
            response.Length = length;
        }

        public static void PullingPackage(
            this PollingResponse response,
            long length)
        {
            ArgumentNullException.ThrowIfNull(response);
            response.Complete = false;
            response.Status = PollingResponseStatus.Pulling;
            response.Err = string.Empty;
            response.Position = 0;
            response.Length = length;
            response.StartTime = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow);
        }

        public static void PullingGit(this PollingResponse response)
        {
            ArgumentNullException.ThrowIfNull(response);
            response.Complete = false;
            response.Status = PollingResponseStatus.Pulling;
            response.Err = string.Empty;
            response.StartTime = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow);
        }

        public static void PullingPackageUpdatePosition(
            this PollingResponse response,
            long position)
        {
            ArgumentNullException.ThrowIfNull(response);
            response.Position = position;
        }
    }

#pragma warning restore CS1591

}
