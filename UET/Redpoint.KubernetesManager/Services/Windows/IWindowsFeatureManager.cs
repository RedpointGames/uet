namespace Redpoint.KubernetesManager.Services.Windows
{
    using System.Threading.Tasks;

    internal interface IWindowsFeatureManager
    {
        Task EnsureRequiredFeaturesAreInstalled(bool isController, CancellationToken cancellationToken);
    }
}
