namespace Redpoint.Uet.SdkManagement.Sdk.Discovery
{
    using Redpoint.Uet.SdkManagement.Sdk.Confidential;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using System.Text;

    public interface ISdkSetupDiscovery
    {
        IAsyncEnumerable<ISdkSetup> DiscoverApplicableSdkSetups(string enginePath);
    }
}
