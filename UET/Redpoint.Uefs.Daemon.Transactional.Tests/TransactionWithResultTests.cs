namespace Redpoint.Uefs.Daemon.Transactional.Tests
{
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.Concurrency;
    using Redpoint.Uefs.Daemon.Transactional.Abstractions;
    using System.Threading;
    using Xunit;

    public class TransactionWithResultTests
    {
        private class TransactionResult
        {
            public required string Data;
        }

        private class TestTransactionRequest : ITransactionRequest<TransactionResult>
        {
            public required string Data;

            public required Gate ResetEvent;
        }

        private class TestTransactionExecutor : ITransactionExecutor<TestTransactionRequest, TransactionResult>
        {
            public async Task<TransactionResult> ExecuteTransactionAsync(
                ITransactionContext<TransactionResult> context,
                TestTransactionRequest transactionRequest,
                CancellationToken cancellationToken)
            {
                await transactionRequest.ResetEvent.WaitAsync(cancellationToken);
                return new TransactionResult { Data = transactionRequest.Data };
            }
        }

        private class TestTransactionDeduplicator : ITransactionDeduplicator<TestTransactionRequest>
        {
            public bool IsDuplicateRequest(TestTransactionRequest incomingRequest, ITransaction<TestTransactionRequest> currentTransaction)
            {
                return true;
            }
        }

        private class ThrowingTransactionRequest : ITransactionRequest<TransactionResult>
        {
        }

        private class ThrowingTransactionExecutor : ITransactionExecutor<ThrowingTransactionRequest, TransactionResult>
        {
            public Task<TransactionResult> ExecuteTransactionAsync(
                ITransactionContext<TransactionResult> context,
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
            services.AddLogging();
            services.AddUefsDaemonTransactional();
            services.AddSingleton<ITransactionExecutor<TestTransactionRequest, TransactionResult>, TestTransactionExecutor>();

            var sp = services.BuildServiceProvider();

            var factory = sp.GetRequiredService<ITransactionalDatabaseFactory>();
            var database = factory.CreateTransactionalDatabase();

            var resetEvent = new Gate();
            var transaction1 = await database.BeginTransactionAsync<TestTransactionRequest, TransactionResult>(
                new TestTransactionRequest { Data = "test", ResetEvent = resetEvent },
                (_, _) => Task.CompletedTask,
                CancellationToken.None)!;
            resetEvent.Open();
            Assert.Equal("test", (await transaction1.WaitForCompletionAsync(CancellationToken.None)).Data);
        }

        [Fact]
        public async Task TransactionCancelledWhenNoListeners()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddUefsDaemonTransactional();
            services.AddSingleton<ITransactionExecutor<TestTransactionRequest, TransactionResult>, TestTransactionExecutor>();

            var sp = services.BuildServiceProvider();

            var factory = sp.GetRequiredService<ITransactionalDatabaseFactory>();
            var database = factory.CreateTransactionalDatabase();

            var resetEvent = new Gate();
            var transaction1 = await database.BeginTransactionAsync<TestTransactionRequest, TransactionResult>(
                new TestTransactionRequest { Data = "test", ResetEvent = resetEvent },
                (_, _) => Task.CompletedTask,
                CancellationToken.None)!;
            await transaction1.DisposeAsync();

            await Assert.ThrowsAsync<TaskCanceledException>(async () =>
            {
                await transaction1.WaitForCompletionAsync(CancellationToken.None);
            });
        }

        [Fact]
        public async Task DeduplicatedTransactionCompletesWhenDeduplicatedTakesOver()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddUefsDaemonTransactional();
            services.AddSingleton<ITransactionExecutor<TestTransactionRequest, TransactionResult>, TestTransactionExecutor>();
            services.AddSingleton<ITransactionDeduplicator<TestTransactionRequest>, TestTransactionDeduplicator>();

            var sp = services.BuildServiceProvider();

            var factory = sp.GetRequiredService<ITransactionalDatabaseFactory>();
            var database = factory.CreateTransactionalDatabase();

            var resetEvent = new Gate();
            var transaction1 = await database.BeginTransactionAsync<TestTransactionRequest, TransactionResult>(
                new TestTransactionRequest { Data = "test", ResetEvent = resetEvent },
                (_, _) => Task.CompletedTask,
                CancellationToken.None)!;
            var transaction2 = await database.BeginTransactionAsync<TestTransactionRequest, TransactionResult>(
                new TestTransactionRequest { Data = "test", ResetEvent = resetEvent },
                (_, _) => Task.CompletedTask,
                CancellationToken.None)!;
            await transaction1.DisposeAsync();
            resetEvent.Open();
            Assert.Equal("test", (await transaction2.WaitForCompletionAsync(CancellationToken.None)).Data);
        }

        [Fact]
        public async Task DeduplicatedTransactionCompletesWhenDeduplicatedCancelled()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddUefsDaemonTransactional();
            services.AddSingleton<ITransactionExecutor<TestTransactionRequest, TransactionResult>, TestTransactionExecutor>();
            services.AddSingleton<ITransactionDeduplicator<TestTransactionRequest>, TestTransactionDeduplicator>();

            var sp = services.BuildServiceProvider();

            var factory = sp.GetRequiredService<ITransactionalDatabaseFactory>();
            var database = factory.CreateTransactionalDatabase();

            var resetEvent = new Gate();
            var transaction1 = await database.BeginTransactionAsync<TestTransactionRequest, TransactionResult>(
                new TestTransactionRequest { Data = "test", ResetEvent = resetEvent },
                (_, _) => Task.CompletedTask,
                CancellationToken.None)!;
            var transaction2 = await database.BeginTransactionAsync<TestTransactionRequest, TransactionResult>(
                new TestTransactionRequest { Data = "test", ResetEvent = resetEvent },
                (_, _) => Task.CompletedTask,
                CancellationToken.None)!;
            await transaction2.DisposeAsync();
            resetEvent.Open();
            Assert.Equal("test", (await transaction1.WaitForCompletionAsync(CancellationToken.None)).Data);
        }

        [Fact]
        public async Task DeduplicatedTransactionCancelledWhenNoListeners()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddUefsDaemonTransactional();
            services.AddSingleton<ITransactionExecutor<TestTransactionRequest, TransactionResult>, TestTransactionExecutor>();
            services.AddSingleton<ITransactionDeduplicator<TestTransactionRequest>, TestTransactionDeduplicator>();

            var sp = services.BuildServiceProvider();

            var factory = sp.GetRequiredService<ITransactionalDatabaseFactory>();
            var database = factory.CreateTransactionalDatabase();

            var resetEvent = new Gate();
            var transaction1 = await database.BeginTransactionAsync<TestTransactionRequest, TransactionResult>(
                new TestTransactionRequest { Data = "test", ResetEvent = resetEvent },
                (_, _) => Task.CompletedTask,
                CancellationToken.None)!;
            var transaction2 = await database.BeginTransactionAsync<TestTransactionRequest, TransactionResult>(
                new TestTransactionRequest { Data = "test", ResetEvent = resetEvent },
                (_, _) => Task.CompletedTask,
                CancellationToken.None)!;
            await transaction1.DisposeAsync();
            await transaction2.DisposeAsync();

            await Assert.ThrowsAsync<TaskCanceledException>(async () =>
            {
                await transaction1.WaitForCompletionAsync(CancellationToken.None);
            });
            await Assert.ThrowsAsync<TaskCanceledException>(async () =>
            {
                await transaction2.WaitForCompletionAsync(CancellationToken.None);
            });
        }

        [Fact]
        public async Task ExceptionFromThrowingExecutorIsCaughtDuringWait()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddUefsDaemonTransactional();
            services.AddSingleton<ITransactionExecutor<ThrowingTransactionRequest, TransactionResult>, ThrowingTransactionExecutor>();

            var sp = services.BuildServiceProvider();

            var factory = sp.GetRequiredService<ITransactionalDatabaseFactory>();
            var database = factory.CreateTransactionalDatabase();

            var transaction1 = await database.BeginTransactionAsync<ThrowingTransactionRequest, TransactionResult>(
                new ThrowingTransactionRequest(),
                (_, _) => Task.CompletedTask,
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
            services.AddLogging();
            services.AddUefsDaemonTransactional();
            services.AddSingleton<ITransactionExecutor<ThrowingTransactionRequest, TransactionResult>, ThrowingTransactionExecutor>();

            var sp = services.BuildServiceProvider();

            var factory = sp.GetRequiredService<ITransactionalDatabaseFactory>();
            var database = factory.CreateTransactionalDatabase();

            var transaction1 = await database.BeginTransactionAsync<ThrowingTransactionRequest, TransactionResult>(
                new ThrowingTransactionRequest(),
                (_, _) => Task.CompletedTask,
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
            services.AddLogging();
            services.AddUefsDaemonTransactional();
            services.AddSingleton<ITransactionExecutor<ThrowingTransactionRequest, TransactionResult>, ThrowingTransactionExecutor>();
            services.AddSingleton<ITransactionDeduplicator<ThrowingTransactionRequest>, ThrowingTransactionDeduplicator>();

            var sp = services.BuildServiceProvider();

            var factory = sp.GetRequiredService<ITransactionalDatabaseFactory>();
            var database = factory.CreateTransactionalDatabase();

            var transaction1 = await database.BeginTransactionAsync<ThrowingTransactionRequest, TransactionResult>(
                new ThrowingTransactionRequest(),
                (_, _) => Task.CompletedTask,
                CancellationToken.None)!;
            var transaction2 = await database.BeginTransactionAsync<ThrowingTransactionRequest, TransactionResult>(
                new ThrowingTransactionRequest(),
                (_, _) => Task.CompletedTask,
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
            services.AddLogging();
            services.AddUefsDaemonTransactional();
            services.AddSingleton<ITransactionExecutor<ThrowingTransactionRequest, TransactionResult>, ThrowingTransactionExecutor>();
            services.AddSingleton<ITransactionDeduplicator<ThrowingTransactionRequest>, ThrowingTransactionDeduplicator>();

            var sp = services.BuildServiceProvider();

            var factory = sp.GetRequiredService<ITransactionalDatabaseFactory>();
            var database = factory.CreateTransactionalDatabase();

            var transaction1 = await database.BeginTransactionAsync<ThrowingTransactionRequest, TransactionResult>(
                new ThrowingTransactionRequest(),
                (_, _) => Task.CompletedTask,
                CancellationToken.None)!;
            var transaction2 = await database.BeginTransactionAsync<ThrowingTransactionRequest, TransactionResult>(
                new ThrowingTransactionRequest(),
                (_, _) => Task.CompletedTask,
                CancellationToken.None)!;
            await transaction2.DisposeAsync();
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await transaction1.DisposeAsync();
            });
        }
    }
}