namespace Redpoint.KubernetesManager.Services
{
    internal interface IKubeConfigManager
    {
        string GetKubeconfigPath(string category, string name);

        Task<string> EnsureGeneratedForNodeAsync(string certificateAuthorityPem, string nodeName);
    }
}
