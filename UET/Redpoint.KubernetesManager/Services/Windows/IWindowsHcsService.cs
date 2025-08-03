namespace Redpoint.KubernetesManager.Services.Windows
{
    using Redpoint.KubernetesManager.Models.Hcs;
    using System.Runtime.Versioning;

    [SupportedOSPlatform("windows")]
    public interface IWindowsHcsService
    {
        HcsComputeSystemWithId[] GetHcsComputeSystems();

        void TerminateHcsSystem(string id);
    }
}