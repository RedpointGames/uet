namespace Redpoint.CloudFramework.Tests
{
    using Redpoint.Collections;
    using System.Linq;
    using System.Threading.Tasks;
    using Xunit;

    public class SelectFastTests
    {
        [Fact]
        public async Task SelectFast()
        {
            var inputs = new[]
            {
                1, 2, 3, 4, 5, 6
            };

            await inputs.ToAsyncEnumerable().SelectFastAwait(async input =>
            {
                Assert.NotEqual(0, input);
                await Task.Delay(input * 10).ConfigureAwait(true);
                return input;
            }).ToListAsync(cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);
        }
    }
}
