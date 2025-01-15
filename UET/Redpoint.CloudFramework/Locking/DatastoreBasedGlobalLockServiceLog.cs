namespace Redpoint.CloudFramework.Locking
{
    using Microsoft.Extensions.Logging;

    internal static partial class DatastoreBasedGlobalLockServiceLog
    {
        [LoggerMessage(
            EventId = 0,
            Level = LogLevel.Information,
            Message = "Beginning acquisition of lock {ns}/{objectToLockName}...")]
        public static partial void LogBeginningAcquisitionOfLock(
            this ILogger logger, string ns, string? objectToLockName);

        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Information,
            Message = "{acquisitionGuid}: Begun transaction for {ns}/{objectToLockName}...")]
        public static partial void LogBegunTransaction(
            this ILogger logger, string? acquisitionGuid, string ns, string? objectToLockName);

        [LoggerMessage(
            EventId = 2,
            Level = LogLevel.Information,
            Message = "{acquisitionGuid}: Loading existing lock model for {ns}/{objectToLockName}...")]
        public static partial void LogLoadingExistingLockModel(
            this ILogger logger, string? acquisitionGuid, string ns, string? objectToLockName);

        [LoggerMessage(
            EventId = 3,
            Level = LogLevel.Information,
            Message = "{acquisitionGuid}: No existing lock object for {ns}/{objectToLockName}, creating new lock model...")]
        public static partial void LogNoExistingLockObject(
            this ILogger logger, string? acquisitionGuid, string ns, string? objectToLockName);

        [LoggerMessage(
            EventId = 4,
            Level = LogLevel.Information,
            Message = "{acquisitionGuid}: Found existing lock object for {ns}/{objectToLockName}, checking expiry...")]
        public static partial void LogFoundExistingLockObject(
            this ILogger logger, string? acquisitionGuid, string ns, string? objectToLockName);

        [LoggerMessage(
            EventId = 5,
            Level = LogLevel.Information,
            Message = "{acquisitionGuid}: Existing lock {ns}/{objectToLockName} has naturally expired, taking...")]
        public static partial void LogExistingLockNaturallyExpired(
            this ILogger logger, string? acquisitionGuid, string ns, string? objectToLockName);

        [LoggerMessage(
            EventId = 6,
            Level = LogLevel.Information,
            Message = "{acquisitionGuid}: Existing lock {ns}/{objectToLockName} still in use, throwing...")]
        public static partial void LogExistingLockStillInUseThrowing(
            this ILogger logger, string? acquisitionGuid, string ns, string? objectToLockName);

        [LoggerMessage(
            EventId = 7,
            Level = LogLevel.Information,
            Message = "{acquisitionGuid}: Attempting commit of transaction for {ns}/{objectToLockName}...")]
        public static partial void LogAttemptingTransactionCommit(
            this ILogger logger, string? acquisitionGuid, string ns, string? objectToLockName);

        [LoggerMessage(
            EventId = 8,
            Level = LogLevel.Information,
            Message = "{acquisitionGuid}: Successful commit of transaction for {ns}/{objectToLockName}, returning lock handle...")]
        public static partial void LogSuccessfulTransactionCommit(
            this ILogger logger, string? acquisitionGuid, string ns, string? objectToLockName);

        [LoggerMessage(
            EventId = 9,
            Level = LogLevel.Information,
            Message = "{acquisitionGuid}: Encountered lock contention while acquiring {ns}/{objectToLockName}...")]
        public static partial void LogEncounteredLogContention(
            this ILogger logger, string? acquisitionGuid, string ns, string? objectToLockName);

        [LoggerMessage(
            EventId = 10,
            Level = LogLevel.Information,
            Message = "{acquisitionGuid}: Encountered disappearing lock while acquiring {ns}/{objectToLockName}...")]
        public static partial void LogEncounteredDisappearingLock(
            this ILogger logger, string? acquisitionGuid, string ns, string? objectToLockName);

        [LoggerMessage(
            EventId = 11,
            Level = LogLevel.Information,
            Message = "{acquisitionGuid}: Reached finally block for {ns}/{objectToLockName}...")]
        public static partial void LogReachedFinallyBlock(
            this ILogger logger, string? acquisitionGuid, string ns, string? objectToLockName);

        [LoggerMessage(
            EventId = 12,
            Level = LogLevel.Information,
            Message = "{acquisitionGuid}: Attempting rollback of transaction for {ns}/{objectToLockName}...")]
        public static partial void LogAttemptingRollbackTransaction(
            this ILogger logger, string? acquisitionGuid, string ns, string? objectToLockName);

        [LoggerMessage(
            EventId = 13,
            Level = LogLevel.Information,
            Message = "{acquisitionGuid}: Transaction rollback completed for {ns}/{objectToLockName}...")]
        public static partial void LogSuccessfulRollbackTransaction(
            this ILogger logger, string? acquisitionGuid, string ns, string? objectToLockName);

        [LoggerMessage(
            EventId = 14,
            Level = LogLevel.Information,
            Message = "{acquisitionGuid}: Lock handle created for {ns}/{objectToLockName}...")]
        public static partial void LogLockHandleCreated(
            this ILogger logger, string? acquisitionGuid, string ns, string? objectToLockName);

        [LoggerMessage(
            EventId = 15,
            Level = LogLevel.Information,
            Message = "{acquisitionGuid}: Automatic renewal task running for {ns}/{objectToLockName}...")]
        public static partial void LogAutomaticRenewalTaskRunning(
            this ILogger logger, string? acquisitionGuid, string ns, string? objectToLockName);

        [LoggerMessage(
            EventId = 16,
            Level = LogLevel.Information,
            Message = "{acquisitionGuid}: Lock handle is not released {ns}/{objectToLockName}, delaying for {milliseconds}ms...")]
        public static partial void LogLockHandleIsNotReleased(
            this ILogger logger, string? acquisitionGuid, string ns, string? objectToLockName, int milliseconds);

        [LoggerMessage(
            EventId = 17,
            Level = LogLevel.Information,
            Message = "{acquisitionGuid}: Lock handle was released since renewal delay began for {ns}/{objectToLockName}...")]
        public static partial void LogLockHandleWasReleasedSinceRenewalDelayBegan(
            this ILogger logger, string? acquisitionGuid, string ns, string? objectToLockName);

        [LoggerMessage(
            EventId = 18,
            Level = LogLevel.Information,
            Message = "{acquisitionGuid}: Beginning renewal transaction for {ns}/{objectToLockName}...")]
        public static partial void LogBeginningRenewalTransaction(
            this ILogger logger, string? acquisitionGuid, string ns, string? objectToLockName);

        [LoggerMessage(
            EventId = 19,
            Level = LogLevel.Information,
            Message = "{acquisitionGuid}: Begun renewal transaction for {ns}/{objectToLockName}...")]
        public static partial void LogBegunRenewalTransaction(
            this ILogger logger, string? acquisitionGuid, string ns, string? objectToLockName);

        [LoggerMessage(
            EventId = 20,
            Level = LogLevel.Information,
            Message = "{acquisitionGuid}: Loading existing lock model for {ns}/{objectToLockName} (renewal)...")]
        public static partial void LogLoadingExistingLockModelRenewal(
            this ILogger logger, string? acquisitionGuid, string ns, string? objectToLockName);

        [LoggerMessage(
            EventId = 21,
            Level = LogLevel.Warning,
            Message = "{acquisitionGuid}: Unreleased lock {ns}/{objectToLockName} during renewal appears to have been acquired and released by someone else!")]
        public static partial void LogUnreleasedLockDuringRenewalAcquiredAndReleasedElsewhere(
            this ILogger logger, string? acquisitionGuid, string ns, string? objectToLockName);

        [LoggerMessage(
            EventId = 22,
            Level = LogLevel.Information,
            Message = "{acquisitionGuid}: Updating the expiry time on {ns}/{objectToLockName}...")]
        public static partial void LogUpdatingExpiryTime(
            this ILogger logger, string? acquisitionGuid, string ns, string? objectToLockName);

        [LoggerMessage(
            EventId = 23,
            Level = LogLevel.Warning,
            Message = "{acquisitionGuid}: Unreleased lock {ns}/{objectToLockName} during renewal appears to have been acquired by someone else!")]
        public static partial void LogUnreleasedLockDuringRenewalAcquiredElsewhere(
            this ILogger logger, string? acquisitionGuid, string ns, string? objectToLockName);

        [LoggerMessage(
            EventId = 24,
            Level = LogLevel.Information,
            Message = "{acquisitionGuid}: Attempting commit of renewal transaction for {ns}/{objectToLockName}...")]
        public static partial void LogAttemptingCommitRenewalTransaction(
            this ILogger logger, string? acquisitionGuid, string ns, string? objectToLockName);

        [LoggerMessage(
            EventId = 25,
            Level = LogLevel.Information,
            Message = "{acquisitionGuid}: Successful commit of renewal transaction for {ns}/{objectToLockName}...")]
        public static partial void LogSuccessfulCommitRenewalTransaction(
            this ILogger logger, string? acquisitionGuid, string ns, string? objectToLockName);

        [LoggerMessage(
            EventId = 26,
            Level = LogLevel.Critical,
            Message = "{acquisitionGuid}: Exception during renewal of {ns}/{objectToLockName}...")]
        public static partial void LogExceptionDuringRenewal(
            this ILogger logger, Exception ex, string? acquisitionGuid, string ns, string? objectToLockName);

        [LoggerMessage(
            EventId = 27,
            Level = LogLevel.Information,
            Message = "{acquisitionGuid}: Reached finally block for renewal of {ns}/{objectToLockName}...")]
        public static partial void LogReachedFinallyBlockRenewal(
            this ILogger logger, string? acquisitionGuid, string ns, string? objectToLockName);

        [LoggerMessage(
            EventId = 28,
            Level = LogLevel.Information,
            Message = "{acquisitionGuid}: Attempting rollback of renewal transaction for {ns}/{objectToLockName}...")]
        public static partial void LogAttemptingRollbackTransactionRenewal(
            this ILogger logger, string? acquisitionGuid, string ns, string? objectToLockName);

        [LoggerMessage(
            EventId = 29,
            Level = LogLevel.Information,
            Message = "{acquisitionGuid}: Renewal transaction rollback completed for {ns}/{objectToLockName}...")]
        public static partial void LogSuccessfulRollbackTransactionRenewal(
            this ILogger logger, string? acquisitionGuid, string ns, string? objectToLockName);

        [LoggerMessage(
            EventId = 30,
            Level = LogLevel.Information,
            Message = "{acquisitionGuid}: Automatic renewal task finished for {ns}/{objectToLockName}...")]
        public static partial void LogAutomaticRenewalTaskFinished(
            this ILogger logger, string? acquisitionGuid, string ns, string? objectToLockName);

        [LoggerMessage(
            EventId = 31,
            Level = LogLevel.Information,
            Message = "{acquisitionGuid}: Starting release of lock {ns}/{objectToLockName}...")]
        public static partial void LogStartingReleaseOfLock(
            this ILogger logger, string? acquisitionGuid, string ns, string? objectToLockName);

        [LoggerMessage(
            EventId = 32,
            Level = LogLevel.Information,
            Message = "{acquisitionGuid}: Beginning release transaction for {ns}/{objectToLockName}...")]
        public static partial void LogBeginningReleaseTransaction(
            this ILogger logger, string? acquisitionGuid, string ns, string? objectToLockName);

        [LoggerMessage(
            EventId = 33,
            Level = LogLevel.Information,
            Message = "{acquisitionGuid}: Begun release transaction for {ns}/{objectToLockName}...")]
        public static partial void LogBegunReleaseTransaction(
            this ILogger logger, string? acquisitionGuid, string ns, string? objectToLockName);

        [LoggerMessage(
            EventId = 34,
            Level = LogLevel.Information,
            Message = "{acquisitionGuid}: Loading existing lock model for {ns}/{objectToLockName} (release)...")]
        public static partial void LogLoadingExistingLockModelForRelease(
            this ILogger logger, string? acquisitionGuid, string ns, string? objectToLockName);

        [LoggerMessage(
            EventId = 35,
            Level = LogLevel.Warning,
            Message = "{acquisitionGuid}: Unreleased lock {ns}/{objectToLockName} during release appears to have been acquired and released by someone else!")]
        public static partial void LogUnreleasedLockDuringReleaseAcquiredAndReleasedElsewhere(
            this ILogger logger, string? acquisitionGuid, string ns, string? objectToLockName);

        [LoggerMessage(
            EventId = 36,
            Level = LogLevel.Information,
            Message = "{acquisitionGuid}: Deleting the lock model for {ns}/{objectToLockName}...")]
        public static partial void LogDeletingLockModel(
            this ILogger logger, string? acquisitionGuid, string ns, string? objectToLockName);

        [LoggerMessage(
            EventId = 37,
            Level = LogLevel.Warning,
            Message = "{acquisitionGuid}: Unreleased lock {ns}/{objectToLockName} during release appears to have been acquired by someone else!")]
        public static partial void LogUnreleasedLockDuringReleasedAcquiredElsewhere(
            this ILogger logger, string? acquisitionGuid, string ns, string? objectToLockName);

        [LoggerMessage(
            EventId = 38,
            Level = LogLevel.Information,
            Message = "{acquisitionGuid}: Attempting commit of release transaction for {ns}/{objectToLockName}...")]
        public static partial void LogAttemptingCommitReleaseTransaction(
            this ILogger logger, string? acquisitionGuid, string ns, string? objectToLockName);

        [LoggerMessage(
            EventId = 39,
            Level = LogLevel.Information,
            Message = "{acquisitionGuid}: Successful commit of release transaction for {ns}/{objectToLockName}...")]
        public static partial void LogSuccessfulCommitReleaseTransaction(
            this ILogger logger, string? acquisitionGuid, string ns, string? objectToLockName);

        [LoggerMessage(
            EventId = 40,
            Level = LogLevel.Critical,
            Message = "{acquisitionGuid}: Exception during release of {ns}/{objectToLockName}...")]
        public static partial void LogExceptionDuringRelease(
            this ILogger logger, Exception ex, string? acquisitionGuid, string ns, string? objectToLockName);

        [LoggerMessage(
            EventId = 41,
            Level = LogLevel.Information,
            Message = "{acquisitionGuid}: Reached finally block for release of {ns}/{objectToLockName}...")]
        public static partial void LogReachedFinallyBlockRelease(
            this ILogger logger, string? acquisitionGuid, string ns, string? objectToLockName);

        [LoggerMessage(
            EventId = 42,
            Level = LogLevel.Information,
            Message = "{acquisitionGuid}: Attempting rollback of release transaction for {ns}/{objectToLockName}...")]
        public static partial void LogAttemptingRollbackTransactionRelease(
            this ILogger logger, string? acquisitionGuid, string ns, string? objectToLockName);

        [LoggerMessage(
            EventId = 43,
            Level = LogLevel.Information,
            Message = "{acquisitionGuid}: Release transaction rollback completed for {ns}/{objectToLockName}...")]
        public static partial void LogSuccessfulRollbackTransactionRelease(
            this ILogger logger, string? acquisitionGuid, string ns, string? objectToLockName);
    }
}
