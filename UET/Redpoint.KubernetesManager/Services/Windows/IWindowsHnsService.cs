namespace Redpoint.KubernetesManager.Services.Windows
{
    using Redpoint.KubernetesManager.Models.Hns;
    using System.Runtime.Versioning;

    [SupportedOSPlatform("windows")]
    internal interface IWindowsHnsService
    {
        HnsNetworkWithId[] GetHnsNetworks();

        void NewHnsNetwork(HnsNetwork network);

        void DeleteHnsNetwork(string id);

        HnsEndpointWithId[] GetHnsEndpoints();

        HnsPolicyListWithId[] GetHnsPolicyLists();

        void DeleteHnsPolicyList(string id);
    }
}