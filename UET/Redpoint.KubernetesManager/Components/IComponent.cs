namespace Redpoint.KubernetesManager.Components
{
    using Redpoint.KubernetesManager.Signalling;

    internal interface IComponent
    {
        void RegisterSignals(IRegistrationContext context);
    }
}
