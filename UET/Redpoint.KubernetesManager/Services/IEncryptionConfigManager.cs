namespace Redpoint.KubernetesManager.Services
{
    using System.Threading.Tasks;

    internal interface IEncryptionConfigManager
    {
        string EncryptionConfigPath { get; }

        Task Initialize();
    }
}
