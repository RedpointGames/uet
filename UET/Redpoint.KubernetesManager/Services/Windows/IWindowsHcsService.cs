namespace Redpoint.KubernetesManager.Services.Windows
{
    using Redpoint.KubernetesManager.Models.Hcs;
    using System.Runtime.Versioning;

    [SupportedOSPlatform("windows")]
    internal interface IWindowsHcsService
    {
        HcsComputeSystemWithId[] GetHcsComputeSystems();

        void TerminateHcsSystem(string id);
    }
}