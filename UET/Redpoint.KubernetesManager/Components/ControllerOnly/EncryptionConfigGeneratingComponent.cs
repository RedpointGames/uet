namespace Redpoint.KubernetesManager.Components.ControllerOnly
{
    using Redpoint.KubernetesManager.Services;
    using Redpoint.KubernetesManager.Signalling;
    using System.Threading.Tasks;

    /// <summary>
    /// The encryption config generating component generates the "encryption-config.yaml"
    /// file on disk, which is used by Kubernetes to encrypt secrets.
    /// 
    /// It will automatically generate the encryption config using the 
    /// <see cref="IEncryptionConfigManager"/> interface, and then it will raise
    /// the <see cref="WellKnownFlags.EncryptionConfigReady"/> flag.
    /// 
    /// This component only runs on the controller.
    /// </summary>
    internal class EncryptionConfigGeneratingComponent : IComponent
    {
        private readonly IEncryptionConfigManager _encryptionConfigManager;

        public EncryptionConfigGeneratingComponent(IEncryptionConfigManager encryptionConfigManager)
        {
            _encryptionConfigManager = encryptionConfigManager;
        }

        public void RegisterSignals(IRegistrationContext context)
        {
            if (context.Role == RoleType.Controller)
            {
                context.OnSignal(WellKnownSignals.Started, OnStartedAsync);
            }
        }

        private async Task OnStartedAsync(IContext context, IAssociatedData? data, CancellationToken cancellationToken)
        {
            // Generate the encryption config.
            await _encryptionConfigManager.Initialize();

            // Encryption config is now ready on disk.
            context.SetFlag(WellKnownFlags.EncryptionConfigReady, null);
        }
    }
}
