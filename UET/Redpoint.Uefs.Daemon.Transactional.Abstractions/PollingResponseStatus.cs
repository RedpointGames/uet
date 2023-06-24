namespace Redpoint.Uefs.Daemon.Transactional.Abstractions
{
    using Google.Protobuf.WellKnownTypes;
    using Redpoint.Git.Native;
    using Redpoint.Uefs.Protocol;
    using System;

    // @todo: Move this to gRPC enum.
    internal static class PollingResponseStatus
    {
        public const string Waiting = "waiting";
        public const string Starting = "starting";
        public const string Checking = "checking";
        public const string Pulling = "pulling";
        public const string Verifying = "verifying";
        public const string Complete = "complete";
        public const string Error = "error";
    }

    public static class PollingResponseExtensions
    {
        public static void Error(this PollingResponse response, string error)
        {
            response.Err = error;
            response.Complete = true;
            response.Status = PollingResponseStatus.Error;
        }

        public static void Exception(this PollingResponse response, Exception ex)
        {
            response.Err = ex.ToString();
            response.Complete = true;
            response.Status = PollingResponseStatus.Error;
        }

        public static void Init(this PollingResponse response, PollingResponseType type)
        {
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
            response.Complete = false;
            response.Status = PollingResponseStatus.Verifying;
            response.VerifyPackageTotal = totalPackages;
        }

        public static void VerifyingPackage(this PollingResponse response, int packageIndex)
        {
            response.VerifyPackageIndex = packageIndex;
        }

        public static void VerifyingChunk(this PollingResponse response, long length)
        {
            response.Complete = false;
            response.Status = PollingResponseStatus.Verifying;
            response.Length = length;
        }

        public static void VerifyingChunkUpdatePosition(this PollingResponse response, long position)
        {
            response.Position = position;
        }

        public static void VerifyingChunkIncrementFixed(this PollingResponse response)
        {
            response.VerifyChunksFixed++;
        }

        public static void CompleteForVerifying(this PollingResponse response)
        {
            response.Complete = true;
            response.Status = PollingResponseStatus.Complete;
            response.Err = string.Empty;
        }

        public static void CompleteForMount(this PollingResponse response)
        {
            response.Complete = true;
            response.Status = PollingResponseStatus.Complete;
            response.Err = string.Empty;
        }

        public static void CompleteForGit(this PollingResponse response)
        {
            response.Complete = true;
            response.Status = PollingResponseStatus.Complete;
            response.Err = string.Empty;
        }

        public static void CompleteForPackage(
            this PollingResponse response,
            string packagePath,
            string packageHash)
        {
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
            response.Complete = false;
            response.Status = PollingResponseStatus.Pulling;
            response.Err = string.Empty;
            response.Position = 0;
            response.Length = length;
            response.StartTime = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow);
        }

        public static void PullingGit(this PollingResponse response)
        {
            response.Complete = false;
            response.Status = PollingResponseStatus.Pulling;
            response.Err = string.Empty;
            response.StartTime = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow);
        }

        public static void PullingPackageUpdatePosition(
            this PollingResponse response,
            long position)
        {
            response.Position = position;
        }

        public static void ReceiveGitUpdate(
            this PollingResponse op,
            GitFetchProgressInfo progress)
        {
            op.GitServerProgressMessage = progress.ServerProgressMessage ?? string.Empty;
            op.GitTotalObjects = progress.TotalObjects ?? 0;
            op.GitIndexedObjects = progress.IndexedObjects ?? 0;
            op.GitReceivedObjects = progress.ReceivedObjects ?? 0;
            op.GitReceivedBytes = progress.ReceivedBytes ?? 0;
            op.GitSlowFetch = progress.SlowFetch ?? false;
        }
    }
}
