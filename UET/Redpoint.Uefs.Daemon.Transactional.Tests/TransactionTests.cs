namespace Redpoint.Uefs.Daemon.Transactional.Tests
{
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.Uefs.Daemon.Transactional.Abstractions;
    using System.Threading;
    using Xunit;

    public class TransactionTests
    {
        private class TestTransactionRequest : ITransactionRequest
        { 
            public required ManualResetEventSlim ResetEvent;

            public required Action Success;

            public required Action Cancelled;
        }

        private class TestTransactionExecutor : ITransactionExecutor<TestTransactionRequest>
        {
            public Task ExecuteTransactionAsync(
                ITransactionContext context,
                TestTransactionRequest transactionRequest, 
                CancellationToken cancellationToken)
            {
                try
                {
                    transactionRequest.ResetEvent.Wait(cancellationToken);
                    transactionRequest.Success();
                }
                catch (OperationCanceledException)
                {
                    transactionRequest.Cancelled();
                    throw;
                }

                return Task.CompletedTask;
            }
        }

        private class TestTransactionDeduplicator : ITransactionDeduplicator<TestTransactionRequest>
        {
            public bool IsDuplicateRequest(TestTransactionRequest incomingRequest, ITransaction<TestTransactionRequest> currentTransaction)
            {
                return true;
            }
        }

        private class ThrowingTransactionRequest : ITransactionRequest
        {
        }

        private class ThrowingTransactionExecutor : ITransactionExecutor<ThrowingTransactionRequest>
        {
            public Task ExecuteTransactionAsync(
                ITransactionContext context,
                ThrowingTransactionRequest transactionRequest,
                CancellationToken cancellationToken)
            {
                throw new InvalidOperationException();
            }
        }

        private class ThrowingTransactionDeduplicator : ITransactionDeduplicator<ThrowingTransactionRequest>
        {
            public bool IsDuplicateRequest(ThrowingTransactionRequest incomingRequest, ITransaction<ThrowingTransactionRequest> currentTransaction)
            {
                return true;
            }
        }

        [Fact]
        public async Task TransactionCompletes()
        {
            var services = new ServiceCollection();
            services.AddUefsDaemonTransactional();
            services.AddSingleton<ITransactionExecutor<TestTransactionRequest>, TestTransactionExecutor>();

            var sp = services.BuildServiceProvider();

            var factory = sp.GetRequiredService<ITransactionalDatabaseFactory>();
            var database = factory.CreateTransactionalDatabase();

            var didSucceed = 0;
            Action success = () => { didSucceed++; };
            Action cancelled = () => { Assert.False(true, "Transaction should not be cancelled"); };

            var resetEvent = new ManualResetEventSlim();
            var transaction1 = await database.BeginTransactionAsync(
                new TestTransactionRequest { Success = success, Cancelled = cancelled, ResetEvent = resetEvent },
                _ => Task.CompletedTask,
                CancellationToken.None)!;
            resetEvent.Set();
            await transaction1.WaitForCompletionAsync(CancellationToken.None);

            Assert.Equal(1, didSucceed);
        }

        [Fact]
        public async Task TransactionCancelledWhenNoListeners()
        {
            var services = new ServiceCollection();
            services.AddUefsDaemonTransactional();
            services.AddSingleton<ITransactionExecutor<TestTransactionRequest>, TestTransactionExecutor>();

            var sp = services.BuildServiceProvider();

            var factory = sp.GetRequiredService<ITransactionalDatabaseFactory>();
            var database = factory.CreateTransactionalDatabase();

            var didCancel = 0;
            Action success = () => { Assert.False(true, "Transaction should not succeed"); };
            Action cancelled = () => { didCancel++; };

            var resetEvent = new ManualResetEventSlim();
            var transaction1 = await database.BeginTransactionAsync(
                new TestTransactionRequest { Success = success, Cancelled = cancelled, ResetEvent = resetEvent },
                _ => Task.CompletedTask,
                CancellationToken.None)!;
            resetEvent.Set();
            await transaction1.DisposeAsync();

            Assert.Equal(1, didCancel);
        }

        [Fact]
        public async Task DeduplicatedTransactionCompletesWhenDeduplicatedTakesOver()
        {
            var services = new ServiceCollection();
            services.AddUefsDaemonTransactional();
            services.AddSingleton<ITransactionExecutor<TestTransactionRequest>, TestTransactionExecutor>();
            services.AddSingleton<ITransactionDeduplicator<TestTransactionRequest>, TestTransactionDeduplicator>();

            var sp = services.BuildServiceProvider();

            var factory = sp.GetRequiredService<ITransactionalDatabaseFactory>();
            var database = factory.CreateTransactionalDatabase();

            var didSucceed = 0;
            Action success = () => { didSucceed++; };
            Action cancelled = () => { Assert.False(true, "Transaction should not be cancelled"); };

            var resetEvent = new ManualResetEventSlim();
            var transaction1 = await database.BeginTransactionAsync(
                new TestTransactionRequest { Success = success, Cancelled = cancelled, ResetEvent = resetEvent },
                _ => Task.CompletedTask,
                CancellationToken.None)!;
            var transaction2 = await database.BeginTransactionAsync(
                new TestTransactionRequest { Success = success, Cancelled = cancelled, ResetEvent = resetEvent },
                _ => Task.CompletedTask,
                CancellationToken.None)!;
            resetEvent.Set();
            await transaction1.DisposeAsync();
            await transaction2.WaitForCompletionAsync(CancellationToken.None);

            Assert.Equal(1, didSucceed);
        }

        [Fact]
        public async Task DeduplicatedTransactionCompletesWhenDeduplicatedCancelled()
        {
            var services = new ServiceCollection();
            services.AddUefsDaemonTransactional();
            services.AddSingleton<ITransactionExecutor<TestTransactionRequest>, TestTransactionExecutor>();
            services.AddSingleton<ITransactionDeduplicator<TestTransactionRequest>, TestTransactionDeduplicator>();

            var sp = services.BuildServiceProvider();

            var factory = sp.GetRequiredService<ITransactionalDatabaseFactory>();
            var database = factory.CreateTransactionalDatabase();

            var didSucceed = 0;
            Action success = () => { didSucceed++; };
            Action cancelled = () => { Assert.False(true, "Transaction should not be cancelled"); };

            var resetEvent = new ManualResetEventSlim();
            var transaction1 = await database.BeginTransactionAsync(
                new TestTransactionRequest { Success = success, Cancelled = cancelled, ResetEvent = resetEvent },
                _ => Task.CompletedTask,
                CancellationToken.None)!;
            var transaction2 = await database.BeginTransactionAsync(
                new TestTransactionRequest { Success = success, Cancelled = cancelled, ResetEvent = resetEvent },
                _ => Task.CompletedTask,
                CancellationToken.None)!;
            resetEvent.Set();
            await transaction2.DisposeAsync();
            await transaction1.WaitForCompletionAsync(CancellationToken.None);

            Assert.Equal(1, didSucceed);
        }

        [Fact]
        public async Task DeduplicatedTransactionCancelledWhenNoListeners()
        {
            var services = new ServiceCollection();
            services.AddUefsDaemonTransactional();
            services.AddSingleton<ITransactionExecutor<TestTransactionRequest>, TestTransactionExecutor>();
            services.AddSingleton<ITransactionDeduplicator<TestTransactionRequest>, TestTransactionDeduplicator>();

            var sp = services.BuildServiceProvider();

            var factory = sp.GetRequiredService<ITransactionalDatabaseFactory>();
            var database = factory.CreateTransactionalDatabase();

            var didCancel = 0;
            Action success = () => { Assert.False(true, "Transaction should not succeed"); };
            Action cancelled = () => { didCancel++; };

            var resetEvent = new ManualResetEventSlim();
            var transaction1 = await database.BeginTransactionAsync(
                new TestTransactionRequest { Success = success, Cancelled = cancelled, ResetEvent = resetEvent },
                _ => Task.CompletedTask,
                CancellationToken.None)!;
            var transaction2 = await database.BeginTransactionAsync(
                new TestTransactionRequest { Success = success, Cancelled = cancelled, ResetEvent = resetEvent },
                _ => Task.CompletedTask,
                CancellationToken.None)!;
            await transaction1.DisposeAsync();
            await transaction2.DisposeAsync();
            resetEvent.Set();

            Assert.Equal(1, didCancel);
        }

        [Fact]
        public async Task ExceptionFromThrowingExecutorIsCaughtDuringWait()
        {
            var services = new ServiceCollection();
            services.AddUefsDaemonTransactional();
            services.AddSingleton<ITransactionExecutor<ThrowingTransactionRequest>, ThrowingTransactionExecutor>();

            var sp = services.BuildServiceProvider();

            var factory = sp.GetRequiredService<ITransactionalDatabaseFactory>();
            var database = factory.CreateTransactionalDatabase();

            var transaction1 = await database.BeginTransactionAsync(
                new ThrowingTransactionRequest(),
                _ => Task.CompletedTask,
                CancellationToken.None)!;
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await transaction1.WaitForCompletionAsync(CancellationToken.None);
            });
        }

        [Fact]
        public async Task ExceptionFromThrowingExecutorIsCaughtWhenNoListeners()
        {
            var services = new ServiceCollection();
            services.AddUefsDaemonTransactional();
            services.AddSingleton<ITransactionExecutor<ThrowingTransactionRequest>, ThrowingTransactionExecutor>();

            var sp = services.BuildServiceProvider();

            var factory = sp.GetRequiredService<ITransactionalDatabaseFactory>();
            var database = factory.CreateTransactionalDatabase();

            var transaction1 = await database.BeginTransactionAsync(
                new ThrowingTransactionRequest(),
                _ => Task.CompletedTask,
                CancellationToken.None)!;
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await transaction1.DisposeAsync();
            });
        }

        [Fact]
        public async Task ExceptionFromThrowingExecutorIsCaughtByLastListenerToDeregister()
        {
            var services = new ServiceCollection();
            services.AddUefsDaemonTransactional();
            services.AddSingleton<ITransactionExecutor<ThrowingTransactionRequest>, ThrowingTransactionExecutor>();
            services.AddSingleton<ITransactionDeduplicator<ThrowingTransactionRequest>, ThrowingTransactionDeduplicator>();

            var sp = services.BuildServiceProvider();

            var factory = sp.GetRequiredService<ITransactionalDatabaseFactory>();
            var database = factory.CreateTransactionalDatabase();

            var transaction1 = await database.BeginTransactionAsync(
                new ThrowingTransactionRequest(),
                _ => Task.CompletedTask,
                CancellationToken.None)!;
            var transaction2 = await database.BeginTransactionAsync(
                new ThrowingTransactionRequest(),
                _ => Task.CompletedTask,
                CancellationToken.None)!;
            await transaction1.DisposeAsync();
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await transaction2.DisposeAsync();
            });
        }

        [Fact]
        public async Task ExceptionFromThrowingExecutorIsCaughtByLastListenerToDeregisterInReverseOrder()
        {
            var services = new ServiceCollection();
            services.AddUefsDaemonTransactional();
            services.AddSingleton<ITransactionExecutor<ThrowingTransactionRequest>, ThrowingTransactionExecutor>();
            services.AddSingleton<ITransactionDeduplicator<ThrowingTransactionRequest>, ThrowingTransactionDeduplicator>();

            var sp = services.BuildServiceProvider();

            var factory = sp.GetRequiredService<ITransactionalDatabaseFactory>();
            var database = factory.CreateTransactionalDatabase();

            var transaction1 = await database.BeginTransactionAsync(
                new ThrowingTransactionRequest(),
                _ => Task.CompletedTask,
                CancellationToken.None)!;
            var transaction2 = await database.BeginTransactionAsync(
                new ThrowingTransactionRequest(),
                _ => Task.CompletedTask,
                CancellationToken.None)!;
            await transaction2.DisposeAsync();
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await transaction1.DisposeAsync();
            });
        }
    }
}