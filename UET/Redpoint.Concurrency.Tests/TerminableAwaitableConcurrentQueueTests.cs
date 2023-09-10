namespace Redpoint.Concurrency.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public class TerminableAwaitableConcurrentQueueTests
    {
        [Fact]
        public async Task TestTermination()
        {
            var queue = new TerminableAwaitableConcurrentQueue<int>();
            queue.Enqueue(1);
            queue.Enqueue(2);
            queue.Enqueue(3);
            queue.Enqueue(4);
            queue.Terminate();

            var list = await queue.ToListAsync();
            Assert.Equal(new[] { 1, 2, 3, 4 }, list);
        }

        [Fact]
        public async Task TestHugeQueue()
        {
            var cancellationTokenSource = new CancellationTokenSource(5000);

            long seen = 0;

            var queue = new TerminableAwaitableConcurrentQueue<int>();
            var enqueue = Task.Run(() =>
            {
                for (int i = 0; i < 100000; i++)
                {
                    queue.Enqueue(i);
                }
                queue.Terminate();
            });
            var dequeue = Task.Run(async () =>
            {
                bool terminate;
                do
                {
                    (_, terminate) = await queue.TryDequeueAsync(cancellationTokenSource.Token);
                    if (!terminate)
                    {
                        Interlocked.Increment(ref seen);
                    }
                } while (!terminate);
            });
            await Task.WhenAll(enqueue, dequeue);
            Assert.Equal(100000, seen);
        }
    }
}
