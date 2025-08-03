namespace Redpoint.KubernetesManager.Signalling
{
    using System.Diagnostics.CodeAnalysis;
    using System.Threading.Tasks;

    [SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "")]
    public delegate Task SignalDelegate(IContext context, IAssociatedData? data, CancellationToken cancellationToken);

    public interface IRegistrationContext
    {
        void OnSignal(string signalType, SignalDelegate callback);

        RoleType Role { get; }
    }
}
