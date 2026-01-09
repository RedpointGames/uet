namespace Redpoint.KubernetesManager.PxeBoot.Provisioning.Step.Reboot
{
    using System.Threading;
    using System.Threading.Tasks;

    internal interface IReboot
    {
        Task RebootMachine(CancellationToken cancellationToken);
    }
}
