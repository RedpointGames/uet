namespace UET.Commands.Internal.CMakeUbaServer
{
    using System.Xml;
    using UET.Commands.Config;

    internal class UbaCoordinatorKubernetesConfig
    {
        public string? Namespace { get; set; }
        public string? Context { get; set; }
        public string? SmbServer { get; set; }
        public string? SmbShare { get; set; }
        public string? SmbUsername { get; set; }
        public string? SmbPassword { get; set; }

        public static UbaCoordinatorKubernetesConfig ReadFromBuildConfigurationXml(IXmlConfigHelper xmlConfigHelper)
        {
            var document = new XmlDocument();
            document.Load(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Unreal Engine", "UnrealBuildTool", "BuildConfiguration.xml"));

            var config = new UbaCoordinatorKubernetesConfig();
            config.Namespace = xmlConfigHelper.GetValue(document, ["Configuration", "Kubernetes", "Namespace"]);
            config.Context = xmlConfigHelper.GetValue(document, ["Configuration", "Kubernetes", "Context"]);
            config.SmbServer = xmlConfigHelper.GetValue(document, ["Configuration", "Kubernetes", "SmbServer"]);
            config.SmbShare = xmlConfigHelper.GetValue(document, ["Configuration", "Kubernetes", "SmbShare"]);
            config.SmbUsername = xmlConfigHelper.GetValue(document, ["Configuration", "Kubernetes", "SmbUsername"]);
            config.SmbPassword = xmlConfigHelper.GetValue(document, ["Configuration", "Kubernetes", "SmbPassword"]);
            return config;
        }
    }
}
