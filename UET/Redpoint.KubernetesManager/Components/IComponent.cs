namespace Redpoint.KubernetesManager.Components
{
    using Redpoint.KubernetesManager.Signalling;

    public interface IComponent
    {
        void RegisterSignals(IRegistrationContext context);
    }
}
