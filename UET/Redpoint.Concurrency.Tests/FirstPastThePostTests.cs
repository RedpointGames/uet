namespace Redpoint.Concurrency.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public class FirstPastThePostTests
    {
        private class Result { public required bool R; }

        [Fact]
        public async Task ReceivesResultWhenExpected()
        {
            Result? result = null;
            var fptp = new FirstPastThePost<Result>(new CancellationTokenSource(), 3, r =>
            {
                Assert.NotNull(r);
                result = r;
                return Task.CompletedTask;
            });
            Assert.Null(result);
            Assert.False(fptp.HasReceivedResult);
            await fptp.ReceiveNoResultAsync();
            Assert.Null(result);
            Assert.False(fptp.HasReceivedResult);
            await fptp.ReceiveResultAsync(new Result { R = true });
            Assert.NotNull(result);
            Assert.True(fptp.HasReceivedResult);
            await fptp.ReceiveNoResultAsync();
            Assert.NotNull(result);
            Assert.True(fptp.HasReceivedResult);
        }

        [Fact]
        public async Task ReceivesNoResultWhenExpected()
        {
            bool didGetNoResult = false;
            var fptp = new FirstPastThePost<Result>(new CancellationTokenSource(), 3, r =>
            {
                Assert.Null(r);
                didGetNoResult = true;
                return Task.CompletedTask;
            });
            Assert.False(didGetNoResult);
            Assert.False(fptp.HasReceivedResult);
            await fptp.ReceiveNoResultAsync();
            Assert.False(didGetNoResult);
            Assert.False(fptp.HasReceivedResult);
            await fptp.ReceiveNoResultAsync();
            Assert.False(didGetNoResult);
            Assert.False(fptp.HasReceivedResult);
            await fptp.ReceiveNoResultAsync();
            Assert.True(didGetNoResult);
            Assert.True(fptp.HasReceivedResult);
        }

        [Fact]
        public async Task ThrowsIfTooManyResultsReceived()
        {
            var fptp = new FirstPastThePost<Result>(new CancellationTokenSource(), 3, r =>
            {
                return Task.CompletedTask;
            });
            await fptp.ReceiveNoResultAsync();
            await fptp.ReceiveNoResultAsync();
            await fptp.ReceiveNoResultAsync();
            await Assert.ThrowsAsync<InvalidOperationException>(fptp.ReceiveNoResultAsync);
        }
    }
}
