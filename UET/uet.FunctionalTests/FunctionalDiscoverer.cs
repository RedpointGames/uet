namespace uet.FunctionalTests
{
    using System.Reflection;
    using System.Text.Json;
    using Xunit.Abstractions;
    using Xunit.Sdk;

    public class FunctionalDiscoverer : IXunitTestCaseDiscoverer
    {
        private readonly IMessageSink _messageSink;

        public FunctionalDiscoverer(IMessageSink messageSink)
        {
            _messageSink = messageSink;
        }

        public IEnumerable<IXunitTestCase> Discover(
            ITestFrameworkDiscoveryOptions discoveryOptions,
            ITestMethod testMethod,
            IAttributeInfo factAttribute)
        {
            foreach (var dir in Directory.GetDirectories(Path.Combine(Assembly.GetExecutingAssembly().Location, "..", "..", "..", "..", "Tests")))
            {
                var configPath = Path.Combine(dir, "FunctionalTestConfig.json");
                if (File.Exists(configPath))
                {
                    var config = JsonSerializer.Deserialize<FunctionalTestConfig[]>(File.ReadAllText(configPath))!;
                    foreach (var entry in config)
                    {
                        yield return new SkippableFactTestCase(
                            new string[] { typeof(SkipException).FullName! },
                            _messageSink,
                            discoveryOptions.MethodDisplayOrDefault(),
                            discoveryOptions.MethodDisplayOptionsOrDefault(),
                            testMethod,
                            new[]
                            {
                                new FunctionalTestEntry
                                {
                                    Config = entry,
                                    Name = Path.GetFileName(dir) + "." + entry.Name,
                                    Path = dir,
                                }
                            });
                    }
                }
            }
        }
    }
}