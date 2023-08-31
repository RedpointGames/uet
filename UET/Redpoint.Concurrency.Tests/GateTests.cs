namespace Redpoint.Concurrency.Tests
{
    using System;
    using System.Diagnostics;
    using System.Threading.Tasks;

    public class GateTests
    {
        [Fact]
        public async Task GateWorks()
        {
            var gate = new Gate();

            var isOpen = false;

            var task1 = Task.Run(async () =>
            {
                await gate.WaitAsync(new CancellationTokenSource(TimeSpan.FromMilliseconds(500)).Token);
                Assert.True(isOpen);

                var st = Stopwatch.StartNew();
                await gate.WaitAsync(new CancellationTokenSource(TimeSpan.FromMilliseconds(500)).Token);
                Assert.True(st.ElapsedMilliseconds < 500, "Expected gate WaitAsync to proceed immediately after unlock");
                Assert.True(isOpen);
            });

            var task2 = Task.Run(async () =>
            {
                await Task.Delay(100);
                isOpen = true;
                gate.Open();
            });

            await Task.WhenAll(task1, task2);
        }
    }
}
