namespace uet.FunctionalTests
{
    using Xunit.Sdk;

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    [XunitTestCaseDiscoverer("uet.FunctionalTests.FunctionalDiscoverer", "uet.FunctionalTests")]
    public class FunctionalAttribute : SkippableFactAttribute
    {
    }
}