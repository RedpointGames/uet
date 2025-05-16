namespace Redpoint.KubernetesManager.Signalling
{
    using System.Threading.Tasks;

    internal delegate Task SignalDelegate(IContext context, IAssociatedData? data, CancellationToken cancellationToken);

    internal interface IRegistrationContext
    {
        void OnSignal(string signalType, SignalDelegate callback);

        RoleType Role { get; }
    }
}
