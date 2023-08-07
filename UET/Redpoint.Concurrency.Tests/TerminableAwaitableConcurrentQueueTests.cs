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
    }
}
