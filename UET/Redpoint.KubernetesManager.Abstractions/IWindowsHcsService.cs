namespace Redpoint.KubernetesManager.Abstractions
{
    using Redpoint.KubernetesManager.Abstractions.Hcs;
    using System.Runtime.Versioning;

    [SupportedOSPlatform("windows")]
    public interface IWindowsHcsService
    {
        HcsComputeSystemWithId[] GetHcsComputeSystems();

        void TerminateHcsSystem(string id);
    }
}