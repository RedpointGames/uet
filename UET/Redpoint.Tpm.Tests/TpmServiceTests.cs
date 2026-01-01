namespace Redpoint.Tpm.Tests
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Redpoint.Tpm.Internal;
    using Redpoint.XunitFramework;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;
    using System.Security.Principal;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;

    public class TpmServiceTests
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public TpmServiceTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        private static bool IsAdministrator
        {
            get
            {
                if (OperatingSystem.IsWindows())
                {
                    using (var identity = WindowsIdentity.GetCurrent())
                    {
                        var principal = new WindowsPrincipal(identity);
                        return principal.IsInRole(WindowsBuiltInRole.Administrator);
                    }
                }
                return false;
            }
        }

        [Fact]
        public async Task TestTpmService()
        {
            Assert.SkipWhen(Environment.GetEnvironmentVariable("CI") == "true", "TPM is not accessible on GitHub Actions.");
            Assert.SkipUnless(IsAdministrator, "This test can only be run as Administrator, as it requires access to the TPM.");

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddLogging(builder =>
            {
                builder.ClearProviders();
                builder.SetMinimumLevel(LogLevel.Trace);
                builder.AddXUnit(_testOutputHelper);
            });
            serviceCollection.AddTpm();
            serviceCollection.AddSingleton<IHostApplicationLifetime, TestHostApplicationLifetime>();

            var serviceProvider = serviceCollection.BuildServiceProvider();

            var tpmService = serviceProvider.GetRequiredService<ITpmService>();

            var (ekPublicBytes, aikPublicBytes, handles) = await tpmService.CreateRequestAsync();

            var (envelopingKey, encryptedKey, encryptedData) = tpmService.Authorize(
                ekPublicBytes,
                aikPublicBytes,
                Encoding.ASCII.GetBytes("hello world"));

            var decryptedMessage = Encoding.ASCII.GetString(tpmService.DecryptSecretKey(
                handles,
                envelopingKey,
                encryptedKey,
                encryptedData));
            Assert.Equal("hello world", decryptedMessage);
        }
    }
}
