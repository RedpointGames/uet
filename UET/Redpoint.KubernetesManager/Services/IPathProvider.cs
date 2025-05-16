namespace Redpoint.KubernetesManager.Services
{
    internal interface IPathProvider
    {
        public string RKMRoot { get; }

        public string RKMInstallationId { get; }

        public string RKMVersion { get; }

        void EnsureRKMRoot();
    }
}
