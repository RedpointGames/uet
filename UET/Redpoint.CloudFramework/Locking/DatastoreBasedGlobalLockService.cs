namespace Redpoint.CloudFramework.Locking
{
    using Google.Cloud.Datastore.V1;
    using Grpc.Core;
    using Microsoft.Extensions.Logging;
    using NodaTime;
    using Redpoint.CloudFramework.Metric;
    using Redpoint.CloudFramework.Models;
    using Redpoint.CloudFramework.Prefix;
    using Redpoint.CloudFramework.Repository;
    using Redpoint.CloudFramework.Repository.Layers;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    internal partial class DatastoreBasedGlobalLockService : IGlobalLockService
    {
        private readonly IGlobalPrefix _globalPrefix;
        private readonly ILogger<DatastoreBasedGlobalLockService> _logger;
        private readonly IDatastoreRepositoryLayer _datastoreRepositoryLayer;
        private readonly IMetricService _metricService;

        private static readonly Duration _defaultExpiryDuration = Duration.FromMinutes(5);
        private static readonly Duration _defaultRenewalDuration = Duration.FromSeconds(60);

        private const string _lockAcquireMetric = "rcf/lock_acquire_count";
        private const string _lockContentionFailureMetric = "rcf/lock_contention_failure_count";
        private const string _lockRenewedMetric = "rcf/lock_renewed_count";
        private const string _lockReleaseMetric = "rcf/lock_release_count";

        public DatastoreBasedGlobalLockService(
            IGlobalPrefix globalPrefix,
            ILogger<DatastoreBasedGlobalLockService> logger,
            IDatastoreRepositoryLayer datastoreRepositoryLayer,
            IMetricService metricService)
        {
            _globalPrefix = globalPrefix;
            _logger = logger;
            _datastoreRepositoryLayer = datastoreRepositoryLayer;
            _metricService = metricService;
        }

        public async Task<ILockHandle> Acquire(string @namespace, Key objectToLock)
        {
            var objectToLockName = _globalPrefix.CreateInternal(objectToLock);
            _logger?.LogBeginningAcquisitionOfLock(@namespace, objectToLockName);
            var lockKeyFactory = await _datastoreRepositoryLayer.GetKeyFactoryAsync<DefaultLockModel>(@namespace, null, CancellationToken.None).ConfigureAwait(false);
            var lockKey = lockKeyFactory.CreateKey(objectToLockName);
            var transaction = await _datastoreRepositoryLayer.BeginTransactionAsync(@namespace, Repository.Transaction.TransactionMode.ReadWrite, null, CancellationToken.None).ConfigureAwait(false);
            var acquisitionGuid = Guid.NewGuid().ToString();
            _logger?.LogBegunTransaction(acquisitionGuid, @namespace, objectToLockName);
            var doRollback = false;
            try
            {
                _logger?.LogLoadingExistingLockModel(acquisitionGuid, @namespace, objectToLockName);
                var existingLock = await _datastoreRepositoryLayer.LoadAsync<DefaultLockModel>(@namespace, lockKey, transaction, null, CancellationToken.None).ConfigureAwait(false);
                if (existingLock == null)
                {
                    _logger?.LogNoExistingLockObject(acquisitionGuid, @namespace, objectToLockName);

                    // No existing lock, use create semantics.
                    existingLock = new DefaultLockModel
                    {
                        Key = lockKey,
                        acquisitionGuid = acquisitionGuid,
                        dateExpiresUtc = SystemClock.Instance.GetCurrentInstant().Plus(_defaultExpiryDuration),
                    };

                    await _datastoreRepositoryLayer.CreateAsync(@namespace, new[] { existingLock }.ToAsyncEnumerable(), transaction, null, CancellationToken.None).FirstAsync().ConfigureAwait(false);
                }
                else
                {
                    _logger?.LogFoundExistingLockObject(acquisitionGuid, @namespace, objectToLockName);

                    // Existing lock, check if expired and use update semantics.
                    if (existingLock.dateExpiresUtc <= SystemClock.Instance.GetCurrentInstant())
                    {
                        _logger?.LogExistingLockNaturallyExpired(acquisitionGuid, @namespace, objectToLockName);

                        // Lock expired, we can take. Update the acquisition GUID (so the original owner
                        // knows they lost the lock if the attempt to renew it).
                        existingLock.acquisitionGuid = acquisitionGuid;
                        existingLock.dateExpiresUtc = SystemClock.Instance.GetCurrentInstant().Plus(_defaultExpiryDuration);

                        await _datastoreRepositoryLayer.UpdateAsync(@namespace, new[] { existingLock }.ToAsyncEnumerable(), transaction, null, CancellationToken.None).FirstAsync().ConfigureAwait(false);
                    }
                    else
                    {
                        _logger?.LogExistingLockStillInUseThrowing(acquisitionGuid, @namespace, objectToLockName);

                        await _metricService.AddPoint(
                            _lockContentionFailureMetric,
                            1,
                            null,
                            new Dictionary<string, string?>
                            {
                                { "namespace", @namespace },
                                { "object_kind", objectToLock.Path.Last().Kind },
                            }).ConfigureAwait(false);

                        throw new LockAcquisitionException(objectToLockName);
                    }
                }

                _logger?.LogAttemptingTransactionCommit(acquisitionGuid, @namespace, objectToLockName);
                await _datastoreRepositoryLayer.CommitAsync(@namespace, transaction, null, CancellationToken.None).ConfigureAwait(false);
                doRollback = false;
                _logger?.LogSuccessfulTransactionCommit(acquisitionGuid, @namespace, objectToLockName);

                await _metricService.AddPoint(
                    _lockAcquireMetric,
                    1,
                    null,
                    new Dictionary<string, string?>
                    {
                        { "namespace", @namespace },
                        { "object_kind", objectToLock.Path.Last().Kind },
                    }).ConfigureAwait(false);

                return new LockHandle(
                    _datastoreRepositoryLayer,
                    _logger,
                    _metricService,
                    existingLock.Key,
                    @namespace,
                    acquisitionGuid,
                    objectToLock.Path.Last().Kind);
            }
            catch (LockAcquisitionException)
            {
                // Just rethrow, we already logged why this was happening.
                throw;
            }
            catch (RpcException ex) when (ex.IsContentionException())
            {
                _logger?.LogEncounteredLogContention(acquisitionGuid, @namespace, objectToLockName);

                await _metricService.AddPoint(
                    _lockContentionFailureMetric,
                    1,
                    null,
                    new Dictionary<string, string?>
                    {
                        { "namespace", @namespace },
                        { "object_kind", objectToLock.Path.Last().Kind },
                    }).ConfigureAwait(false);

                throw new LockAcquisitionException(objectToLockName);
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
            {
                _logger?.LogEncounteredDisappearingLock(acquisitionGuid, @namespace, objectToLockName);

                await _metricService.AddPoint(
                    _lockContentionFailureMetric,
                    1,
                    null,
                    new Dictionary<string, string?>
                    {
                        { "namespace", @namespace },
                        { "object_kind", objectToLock.Path.Last().Kind },
                    }).ConfigureAwait(false);

                throw new LockAcquisitionException(objectToLockName);
            }
            finally
            {
                _logger?.LogReachedFinallyBlock(acquisitionGuid, @namespace, objectToLockName);
                if (doRollback)
                {
                    _logger?.LogAttemptingRollbackTransaction(acquisitionGuid, @namespace, objectToLockName);
                    await _datastoreRepositoryLayer.RollbackAsync(@namespace, transaction, null, CancellationToken.None).ConfigureAwait(false);
                    _logger?.LogSuccessfulRollbackTransaction(acquisitionGuid, @namespace, objectToLockName);
                }
            }
        }

        public async Task AcquireAndUse(string @namespace, Key objectToLock, Func<Task> block)
        {
            await using ((await Acquire(@namespace, objectToLock).ConfigureAwait(false)).ConfigureAwait(false))
            {
                await block().ConfigureAwait(false);
            }
        }

        public async Task<T> AcquireAndUse<T>(string @namespace, Key objectToLock, Func<Task<T>> block)
        {
            await using ((await Acquire(@namespace, objectToLock).ConfigureAwait(false)).ConfigureAwait(false))
            {
                return await block().ConfigureAwait(false);
            }
        }

        private class LockHandle : ILockHandle
        {
            private readonly IDatastoreRepositoryLayer _datastoreRepositoryLayer;
            private readonly ILogger<DatastoreBasedGlobalLockService>? _logger;
            private readonly IMetricService _metricService;
            private readonly string _objectKind;
            private readonly Key _lockKey;
            private readonly string _namespace;
            private readonly string _acquisitionGuid;
            private readonly CancellationTokenSource _cancellationTokenSource;
            private readonly Task _automaticRenewalTask;
            private bool _isReleased;

            public LockHandle(
                IDatastoreRepositoryLayer datastoreRepositoryLayer,
                ILogger<DatastoreBasedGlobalLockService>? logger,
                IMetricService metricService,
                Key realLockKey,
                string @namespace,
                string acquisitionGuid,
                string objectKind)
            {
                _datastoreRepositoryLayer = datastoreRepositoryLayer;
                _logger = logger;
                _lockKey = realLockKey;
                _namespace = @namespace;
                _acquisitionGuid = acquisitionGuid;
                _cancellationTokenSource = new CancellationTokenSource();
                _isReleased = false;
                _metricService = metricService;
                _objectKind = objectKind;

                _logger?.LogLockHandleCreated(_acquisitionGuid, _namespace, _lockKey.GetNameFromKey());

                _automaticRenewalTask = Task.Run(AutomaticRenewal);
            }

            private async Task AutomaticRenewal()
            {
                _logger?.LogAutomaticRenewalTaskRunning(_acquisitionGuid, _namespace, _lockKey.GetNameFromKey());

                while (!_isReleased)
                {
                    _logger?.LogLockHandleIsNotReleased(_acquisitionGuid, _namespace, _lockKey.GetNameFromKey(), (int)_defaultRenewalDuration.TotalMilliseconds);

                    await Task.Delay((int)_defaultRenewalDuration.TotalMilliseconds, _cancellationTokenSource.Token).ConfigureAwait(false);

                    if (_isReleased)
                    {
                        _logger?.LogLockHandleWasReleasedSinceRenewalDelayBegan(_acquisitionGuid, _namespace, _lockKey.GetNameFromKey());

                        await _metricService.AddPoint(
                            _lockReleaseMetric,
                            1,
                            null,
                            new Dictionary<string, string?>
                            {
                                { "namespace", _namespace ?? string.Empty },
                                { "object_kind", _objectKind },
                            }).ConfigureAwait(false);

                        return;
                    }

                    _logger?.LogBeginningRenewalTransaction(_acquisitionGuid, _namespace, _lockKey.GetNameFromKey());
                    var transaction = await _datastoreRepositoryLayer.BeginTransactionAsync(_namespace, Repository.Transaction.TransactionMode.ReadWrite, null, CancellationToken.None).ConfigureAwait(false);
                    _logger?.LogBegunRenewalTransaction(_acquisitionGuid, _namespace, _lockKey.GetNameFromKey());
                    var doRollback = false;
                    try
                    {
                        _logger?.LogLoadingExistingLockModelRenewal(_acquisitionGuid, _namespace, _lockKey.GetNameFromKey());
                        var existingLock = await _datastoreRepositoryLayer.LoadAsync<DefaultLockModel>(_namespace, _lockKey, transaction, null, CancellationToken.None).ConfigureAwait(false);
                        if (existingLock == null)
                        {
                            _logger?.LogUnreleasedLockDuringRenewalAcquiredAndReleasedElsewhere(_acquisitionGuid, _namespace, _lockKey.GetNameFromKey());

                            await _metricService.AddPoint(
                                _lockReleaseMetric,
                                1,
                                null,
                                new Dictionary<string, string?>
                                {
                                    { "namespace", _namespace ?? string.Empty },
                                    { "object_kind", _objectKind },
                                }).ConfigureAwait(false);

                            // No lock? what? Assume someone else took control of the lock because
                            // we let it lapse, and then they were finished with it so they deleted it.
                            // In this case, the lock has been released due to expiry, so bail.
                            _isReleased = true;
                            return;
                        }
                        else
                        {
                            // Existing lock, check if we still have the handle on it.
                            if (existingLock.acquisitionGuid == _acquisitionGuid)
                            {
                                _logger?.LogUpdatingExpiryTime(_acquisitionGuid, _namespace, _lockKey.GetNameFromKey());

                                // Update the lock's expiry time to our current time plus the default expiry.
                                existingLock.dateExpiresUtc = SystemClock.Instance.GetCurrentInstant().Plus(_defaultExpiryDuration);
                                await _datastoreRepositoryLayer.UpdateAsync(_namespace, new[] { existingLock }.ToAsyncEnumerable(), transaction, null, CancellationToken.None).FirstAsync().ConfigureAwait(false);
                            }
                            else
                            {
                                _logger?.LogUnreleasedLockDuringRenewalAcquiredElsewhere(_acquisitionGuid, _namespace, _lockKey.GetNameFromKey());

                                await _metricService.AddPoint(
                                    _lockReleaseMetric,
                                    1,
                                    null,
                                    new Dictionary<string, string?>
                                    {
                                        { "namespace", _namespace ?? string.Empty },
                                        { "object_kind", _objectKind },
                                    }).ConfigureAwait(false);

                                // Someone else now owns the lock! Treat it as released from us.
                                _isReleased = true;
                                return;
                            }
                        }

                        _logger?.LogAttemptingCommitRenewalTransaction(_acquisitionGuid, _namespace, _lockKey.GetNameFromKey());
                        await _datastoreRepositoryLayer.CommitAsync(_namespace, transaction, null, CancellationToken.None).ConfigureAwait(false);
                        doRollback = false;
                        _logger?.LogSuccessfulCommitRenewalTransaction(_acquisitionGuid, _namespace, _lockKey.GetNameFromKey());

                        await _metricService.AddPoint(
                            _lockRenewedMetric,
                            1,
                            null,
                            new Dictionary<string, string?>
                            {
                                { "namespace", _namespace ?? string.Empty },
                                { "object_kind", _objectKind },
                            }).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogExceptionDuringRenewal(ex, _acquisitionGuid, _namespace, _lockKey.GetNameFromKey());
                    }
                    finally
                    {
                        _logger?.LogReachedFinallyBlockRenewal(_acquisitionGuid, _namespace, _lockKey.GetNameFromKey());
                        if (doRollback)
                        {
                            _logger?.LogAttemptingRollbackTransactionRenewal(_acquisitionGuid, _namespace, _lockKey.GetNameFromKey());
                            await _datastoreRepositoryLayer.RollbackAsync(_namespace, transaction, null, CancellationToken.None).ConfigureAwait(false);
                            _logger?.LogSuccessfulRollbackTransactionRenewal(_acquisitionGuid, _namespace, _lockKey.GetNameFromKey());
                        }
                    }
                }

                _logger?.LogAutomaticRenewalTaskFinished(_acquisitionGuid, _namespace, _lockKey.GetNameFromKey());
            }

            public async Task Release()
            {
                await DisposeAsync().ConfigureAwait(false);
            }

            public async ValueTask DisposeAsync()
            {
                if (_isReleased)
                {
                    return;
                }

                try
                {
                    _logger?.LogStartingReleaseOfLock(_acquisitionGuid, _namespace, _lockKey.GetNameFromKey());

                    _isReleased = true;
                    _cancellationTokenSource.Cancel();

                    // Wait 1.5 seconds to ensure that we don't cause Datastore contention with ourselves due to short usage
                    // of the lock or the renewal just happening (we might still get contention from other processes).
                    await Task.Delay(1500).ConfigureAwait(false);

                    _logger?.LogBeginningReleaseTransaction(_acquisitionGuid, _namespace, _lockKey.GetNameFromKey());
                    var transaction = await _datastoreRepositoryLayer.BeginTransactionAsync(_namespace, Repository.Transaction.TransactionMode.ReadWrite, null, CancellationToken.None).ConfigureAwait(false);
                    _logger?.LogBegunReleaseTransaction(_acquisitionGuid, _namespace, _lockKey.GetNameFromKey());
                    var doRollback = false;
                    try
                    {
                        _logger?.LogLoadingExistingLockModelForRelease(_acquisitionGuid, _namespace, _lockKey.GetNameFromKey());

                        var existingLock = await _datastoreRepositoryLayer.LoadAsync<DefaultLockModel>(_namespace, _lockKey, transaction, null, CancellationToken.None).ConfigureAwait(false);
                        if (existingLock == null)
                        {
                            _logger?.LogUnreleasedLockDuringReleaseAcquiredAndReleasedElsewhere(_acquisitionGuid, _namespace, _lockKey.GetNameFromKey());

                            // No lock? Someone else might have already grabbed it and released it (see
                            // the comment in renewal logic).
                            return;
                        }
                        else
                        {
                            // Existing lock, check if we still have the handle on it.
                            if (existingLock.acquisitionGuid == _acquisitionGuid)
                            {
                                _logger?.LogDeletingLockModel(_acquisitionGuid, _namespace, _lockKey.GetNameFromKey());

                                // We can explicitly delete the lock because we still own it.
                                await _datastoreRepositoryLayer.DeleteAsync(_namespace, new[] { existingLock }.ToAsyncEnumerable(), transaction, null, CancellationToken.None).ConfigureAwait(false);
                            }
                            else
                            {
                                _logger?.LogUnreleasedLockDuringReleasedAcquiredElsewhere(_acquisitionGuid, _namespace, _lockKey.GetNameFromKey());

                                // Someone else now owns the lock! Treat it as released from us.
                                _isReleased = true;
                                return;
                            }
                        }

                        _logger?.LogAttemptingCommitReleaseTransaction(_acquisitionGuid, _namespace, _lockKey.GetNameFromKey());
                        await _datastoreRepositoryLayer.CommitAsync(_namespace, transaction, null, CancellationToken.None).ConfigureAwait(false);
                        doRollback = false;
                        _logger?.LogSuccessfulCommitReleaseTransaction(_acquisitionGuid, _namespace, _lockKey.GetNameFromKey());

                        await _metricService.AddPoint(
                            _lockReleaseMetric,
                            1,
                            null,
                            new Dictionary<string, string?>
                            {
                                { "namespace", _namespace ?? string.Empty },
                                { "object_kind", _objectKind },
                            }).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogExceptionDuringRelease(ex, _acquisitionGuid, _namespace, _lockKey.GetNameFromKey());
                        throw;
                    }
                    finally
                    {
                        _logger?.LogReachedFinallyBlockRelease(_acquisitionGuid, _namespace, _lockKey.GetNameFromKey());
                        if (doRollback)
                        {
                            _logger?.LogAttemptingRollbackTransactionRelease(_acquisitionGuid, _namespace, _lockKey.GetNameFromKey());
                            await _datastoreRepositoryLayer.RollbackAsync(_namespace, transaction, null, CancellationToken.None).ConfigureAwait(false);
                            _logger?.LogSuccessfulRollbackTransactionRelease(_acquisitionGuid, _namespace, _lockKey.GetNameFromKey());
                        }
                    }
                }
                finally
                {
                    if (_isReleased)
                    {
                        try
                        {
                            await _automaticRenewalTask.ConfigureAwait(false);
                        }
                        catch
                        {
                        }
                        _cancellationTokenSource.Dispose();
                    }
                }
            }
        }
    }
}
