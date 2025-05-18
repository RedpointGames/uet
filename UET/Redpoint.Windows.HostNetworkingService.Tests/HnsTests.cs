namespace Redpoint.Windows.HostNetworkingService.Tests
{
    using System.Runtime.Versioning;
    using System.Security.Principal;
    using Xunit;

    public class HnsTests
    {
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

        [Fact(Skip = "This test does not pass on the build server, but does pass locally.")]
        [SupportedOSPlatform("windows6.2")]
        public void CanReadHnsNetworks()
        {
            Skip.IfNot(OperatingSystem.IsWindowsVersionAtLeast(6, 2));
            Skip.IfNot(IsAdministrator);

            var api = IHnsApi.GetInstance();

            // Just call this and check that it works.
            try
            {
                _ = api.GetHnsNetworks();
            }
            catch (HnsNotAvailableException)
            {
                // This is also OK.
            }
        }
    }
}
