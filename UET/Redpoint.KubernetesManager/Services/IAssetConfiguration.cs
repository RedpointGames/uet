namespace Redpoint.KubernetesManager.Services
{
    public interface IAssetConfiguration
    {
        string this[string key] { get; }
    }
}
