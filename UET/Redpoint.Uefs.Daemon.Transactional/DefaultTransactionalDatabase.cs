namespace Redpoint.Uefs.Daemon.Transactional
{
    using KeyedSemaphores;
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.Uefs.Daemon.Transactional.Abstractions;
    using System;
    using System.Collections.Generic;
    using System.Runtime.ExceptionServices;
    using System.Threading.Tasks;

    internal class DefaultTransactionalDatabase : ITransactionalDatabase
    {
        private IServiceProvider _serviceProvider;
        internal KeyedSemaphoresCollection<string> _semaphores;
        internal string? _currentMountOperation;

        private readonly SemaphoreSlim _transactionListSemasphore;
        private readonly Dictionary<string, IWaitableTransaction> _transactionList;
        private readonly Dictionary<string, Task> _transactionExecutorTasks;

        public DefaultTransactionalDatabase(
            IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _semaphores = new KeyedSemaphoresCollection<string>();

            _transactionListSemasphore = new SemaphoreSlim(1);
            _transactionList = new Dictionary<string, IWaitableTransaction>();
            _transactionExecutorTasks = new Dictionary<string, Task>();
        }

        public async Task<ITransactionHandle> BeginTransactionAsync<TRequest>(
            TRequest transactionRequest,
            TransactionListenerDelegate transactionListener,
            CancellationToken cancellationToken) where TRequest : class, ITransactionRequest
        {
            var executor = _serviceProvider.GetRequiredService<ITransactionExecutor<TRequest>>();
            var deduplicator = _serviceProvider.GetService<ITransactionDeduplicator<TRequest>>();

            // We must check what transactions are currently in-progress, check if we can
            // deduplicate the current one, and if not, create a transaction for the request.
            await _transactionListSemasphore.WaitAsync(cancellationToken);
            try
            {
                DefaultTransaction<TRequest>? deduplicatedTransaction = null;
                string? deduplicatedTransactionId = null;
                if (deduplicator != null)
                {
                    foreach (var kv in _transactionList)
                    {
                        if (kv.Value is DefaultTransaction<TRequest> transaction)
                        {
                            if (deduplicator.IsDuplicateRequest(transactionRequest, transaction))
                            {
                                deduplicatedTransactionId = kv.Key;
                                deduplicatedTransaction = transaction;
                                break;
                            }
                        }
                    }
                }

                if (deduplicatedTransaction != null)
                {
                    // This is a duplicate transaction. Register the listener against the existing
                    // transaction and return it.
                    return new DefaultTransactionHandle(
                        deduplicatedTransactionId!,
                        deduplicatedTransaction,
                        deduplicatedTransaction.RegisterListener(transactionListener));
                }
                else
                {
                    // Create the new transaction object. Need to generate
                    // an ID (replacing the current background operation
                    // allocation stuff) and add it to the list.
                    var backgroundable = false;
                    if (transactionRequest is IBackgroundableTransactionRequest btr)
                    {
                        backgroundable = btr.NoWait;
                    }
                    var cancellationTokenSource = new CancellationTokenSource();
                    var executorCompleteSemaphore = new SemaphoreSlim(0);
                    var transaction = new DefaultTransaction<TRequest>(
                        transactionRequest,
                        transactionListener,
                        cancellationTokenSource,
                        backgroundable,
                        executorCompleteSemaphore);
                    var id = Guid.NewGuid().ToString();
                    while (!_transactionList.TryAdd(id, transaction))
                    {
                        id = Guid.NewGuid().ToString();
                    }
                    _transactionExecutorTasks.Add(id, Task.Run(async () =>
                    {
                        try
                        {
                            await using (var context = new DefaultTransactionContext(this, transaction))
                            {
                                await executor.ExecuteTransactionAsync(
                                    context,
                                    transactionRequest,
                                    cancellationTokenSource.Token);
                            }
                        }
                        catch (Exception ex)
                        {
                            transaction._thrownException = ExceptionDispatchInfo.Capture(ex);
                            throw;
                        }
                        finally
                        {
                            executorCompleteSemaphore.Release();
                        }
                    }));
                    return new DefaultTransactionHandle(
                        id,
                        transaction,
                        transaction.GetDisposableForInitiallyRegisteredListener());
                }
            }
            finally
            {
                _transactionListSemasphore.Release();
            }
        }

        public async Task<ITransactionHandle<TResult>> BeginTransactionAsync<TRequest, TResult>(
            TRequest transactionRequest,
            TransactionListenerDelegate<TResult> transactionListener,
            CancellationToken cancellationToken) where TRequest : class, ITransactionRequest<TResult> where TResult : class
        {
            var executor = _serviceProvider.GetRequiredService<ITransactionExecutor<TRequest, TResult>>();
            var deduplicator = _serviceProvider.GetService<ITransactionDeduplicator<TRequest>>();

            // We must check what transactions are currently in-progress, check if we can
            // deduplicate the current one, and if not, create a transaction for the request.
            await _transactionListSemasphore.WaitAsync(cancellationToken);
            try
            {
                DefaultTransactionWithResult<TRequest, TResult>? deduplicatedTransaction = null;
                string? deduplicatedTransactionId = null;
                if (deduplicator != null)
                {
                    foreach (var kv in _transactionList)
                    {
                        if (kv.Value is DefaultTransactionWithResult<TRequest, TResult> transaction)
                        {
                            if (deduplicator.IsDuplicateRequest(transactionRequest, transaction))
                            {
                                deduplicatedTransactionId = kv.Key;
                                deduplicatedTransaction = transaction;
                                break;
                            }
                        }
                    }
                }

                if (deduplicatedTransaction != null)
                {
                    // This is a duplicate transaction. Register the listener against the existing
                    // transaction and return it.
                    return new DefaultTransactionHandleWithResult<TResult>(
                        deduplicatedTransactionId!,
                        deduplicatedTransaction,
                        deduplicatedTransaction.RegisterListener(transactionListener));
                }
                else
                {
                    // Create the new transaction object. Need to generate
                    // an ID (replacing the current background operation
                    // allocation stuff) and add it to the list.
                    var backgroundable = false;
                    if (transactionRequest is IBackgroundableTransactionRequest btr)
                    {
                        backgroundable = btr.NoWait;
                    }
                    var cancellationTokenSource = new CancellationTokenSource();
                    var executorCompleteSemaphore = new SemaphoreSlim(0);
                    var transaction = new DefaultTransactionWithResult<TRequest, TResult>(
                        transactionRequest,
                        transactionListener,
                        cancellationTokenSource,
                        backgroundable,
                        executorCompleteSemaphore);
                    var id = Guid.NewGuid().ToString();
                    while (!_transactionList.TryAdd(id, transaction))
                    {
                        id = Guid.NewGuid().ToString();
                    }
                    _transactionExecutorTasks.Add(id, Task.Run(async () =>
                    {
                        try
                        {
                            await using (var context = new DefaultTransactionContextWithResult<TResult>(this, transaction))
                            {
                                var result = await executor.ExecuteTransactionAsync(
                                    context,
                                    transactionRequest,
                                    cancellationTokenSource.Token);
                                transaction.Result = result;
                            }
                        }
                        catch (Exception ex)
                        {
                            transaction._thrownException = ExceptionDispatchInfo.Capture(ex);
                            throw;
                        }
                        finally
                        {
                            executorCompleteSemaphore.Release();
                        }
                    }));
                    return new DefaultTransactionHandleWithResult<TResult>(
                        id,
                        transaction,
                        transaction.GetDisposableForInitiallyRegisteredListener());
                }
            }
            finally
            {
                _transactionListSemasphore.Release();
            }
        }

        public async Task<ITransactionHandle?> AddListenerToExistingTransactionAsync(
            string transactionId, 
            TransactionListenerDelegate transactionListener,
            CancellationToken cancellationToken)
        {
            await _transactionListSemasphore.WaitAsync(cancellationToken);
            try
            {
                if (_transactionList.TryGetValue(transactionId, out var transaction))
                {
                    return new DefaultTransactionHandle(
                        transactionId,
                        transaction,
                        transaction.RegisterListener(transactionListener));
                }

                return null;
            }
            finally
            {
                _transactionListSemasphore.Release();
            }
        }

        public async Task<ITransactionHandle<TResult>?> AddListenerToExistingTransactionAsync<TResult>(
            string transactionId, 
            TransactionListenerDelegate<TResult> transactionListener,
            CancellationToken cancellationToken) where TResult : class
        {
            await _transactionListSemasphore.WaitAsync(cancellationToken);
            try
            {
                if (_transactionList.TryGetValue(transactionId, out var transaction))
                {
                    if (transaction is IWaitableTransaction<TResult> resultTransaction)
                    {
                        return new DefaultTransactionHandleWithResult<TResult>(
                            transactionId,
                            resultTransaction,
                            resultTransaction.RegisterListener(transactionListener));
                    }
                }

                return null;
            }
            finally
            {
                _transactionListSemasphore.Release();
            }
        }
    }
}
