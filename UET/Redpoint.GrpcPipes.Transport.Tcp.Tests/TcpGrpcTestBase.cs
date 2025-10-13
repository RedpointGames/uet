namespace Redpoint.GrpcPipes.Transport.Tcp.Tests
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Xunit;

    public class TcpGrpcTestBase
    {
        private readonly ITestOutputHelper _output;

        internal TcpGrpcTestBase(ITestOutputHelper output)
        {
            _output = output;
        }

        internal ILogger GetLogger()
        {
            var services = new ServiceCollection();
            services.AddLogging(builder =>
            {
                if (Environment.GetEnvironmentVariable("CI") != "true")
                {
                    builder.SetMinimumLevel(LogLevel.Trace);
                    builder.AddXUnit(
                        _output,
                        configure =>
                        {
                        });
                }
            });
            return services.BuildServiceProvider().GetRequiredService<ILogger<TcpGrpcUnary>>();
        }
    }
}
