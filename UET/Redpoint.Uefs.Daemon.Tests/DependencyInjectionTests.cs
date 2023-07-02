namespace Redpoint.Uefs.Daemon.Tests
{
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.Uefs.Daemon;
    using System;
    using System.Collections;

    public class DependencyInjectionTestGenerator : IEnumerable<object[]>
    {
        private static readonly ServiceCollection _serviceCollection;

        static DependencyInjectionTestGenerator()
        {
            _serviceCollection = new ServiceCollection();
            Program.AddAllServices(_serviceCollection, new string[0]);
        }

        public IEnumerator<object[]> GetEnumerator()
        {
            return _serviceCollection
                .Where(x => x.ServiceType.FullName!.StartsWith("Redpoint."))
                // @note: Too much of the UEFS daemon implementations do things in the constructor
                // that require administrative permissions or otherwise conflict with an existing
                // UEFS service running on the same machine.
                .Where(x => !x.ServiceType.FullName!.StartsWith("Redpoint.Uefs.Daemon."))
                .DistinctBy(x => x.ServiceType.FullName)
                .Select(x => new object[] { x.ServiceType })
                .GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    public class DependencyInjectionTests
    {
        private static readonly ServiceProvider _serviceProvider;

        static DependencyInjectionTests()
        {
            var services = new ServiceCollection();
            Program.AddAllServices(services, new string[0]);
            _serviceProvider = services.BuildServiceProvider();
        }

        [Theory]
        [ClassData(typeof(DependencyInjectionTestGenerator))]
        public void CanInitialize(Type type)
        {
            _ = _serviceProvider.GetRequiredService(type);
        }
    }
}