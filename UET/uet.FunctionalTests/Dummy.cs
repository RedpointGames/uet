namespace uet.DummyTests
{
    /// <remarks>
    /// This test class exists so that when we filter out functional tests on the build server,
    /// it doesn't cause the tests to fail due to no tests matching the filter.
    /// </remarks>
    public class Dummy
    {
        [Fact]
        public void DummyTest()
        {
        }
    }
}
