namespace Redpoint.KubernetesManager.Services
{
    using System.Threading.Tasks;

    internal interface IAssetManager
    {
        Task EnsureAsset(string configurationKey, string filename, CancellationToken cancellationToken);

        Task ExtractAsset(string filename, string target, CancellationToken cancellationToken, string? trimLeading = null);
    }
}
