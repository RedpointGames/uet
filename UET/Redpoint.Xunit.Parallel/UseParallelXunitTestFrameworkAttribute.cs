namespace Redpoint.Xunit.Parallel
{
    using global::Xunit.Sdk;

    [TestFrameworkDiscoverer("Redpoint.Xunit.Parallel.ParallelXunitTestFrameworkTypeDiscoverer", "Redpoint.Xunit.Parallel")]
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
    public sealed class UseParallelXunitTestFrameworkAttribute : Attribute, ITestFrameworkAttribute
    {
    }
}