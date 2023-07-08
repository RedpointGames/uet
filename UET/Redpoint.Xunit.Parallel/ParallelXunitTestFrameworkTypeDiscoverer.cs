namespace Redpoint.Xunit.Parallel
{
    using global::Xunit.Abstractions;
    using global::Xunit.Sdk;

    public class ParallelXunitTestFrameworkTypeDiscoverer : ITestFrameworkTypeDiscoverer
    {
        public Type GetTestFrameworkType(IAttributeInfo attribute)
        {
            return typeof(ParallelXunitTestFramework);
        }
    }
}