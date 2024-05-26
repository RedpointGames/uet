namespace Redpoint.Uet.Patching.Runtime.Kubernetes
{
    using System.Reflection;

    internal class KubernetesUbaConfigFromHook : IKubernetesUbaConfig
    {
        private readonly object _ubaKubeConfig;

        public KubernetesUbaConfigFromHook(object ubtHookObject)
        {
            var field = ubtHookObject.GetType().GetField("_ubaKubeConfig", BindingFlags.NonPublic | BindingFlags.Instance);
            _ubaKubeConfig = field!.GetValue(ubtHookObject)!;
        }

        private string? GetPropertyValue(string property)
        {
            return _ubaKubeConfig.GetType()
                .GetProperty(property, BindingFlags.Public | BindingFlags.Instance)
                ?.GetValue(_ubaKubeConfig) as string;
        }

        public string? Namespace => GetPropertyValue("Namespace");

        public string? Context => GetPropertyValue("Context");

        public string? SmbServer => GetPropertyValue("SmbServer");

        public string? SmbShare => GetPropertyValue("SmbShare");

        public string? SmbUsername => GetPropertyValue("SmbUsername");

        public string? SmbPassword => GetPropertyValue("SmbPassword");
    }
}
