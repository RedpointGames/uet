namespace UET.Commands.Config
{
    using System;
    using System.Threading.Tasks;
    using System.Xml;

    internal sealed class UbaPreferRemoteBooleanConfigSetting : IBooleanConfigSetting
    {
        private readonly IXmlConfigHelper _configHelper;

        public UbaPreferRemoteBooleanConfigSetting(IXmlConfigHelper configHelper)
        {
            _configHelper = configHelper;
        }

        public string Name => "uba-prefer-remote";

        public string Description => "When enabled, the Unreal Build Accelerator will not use the local machine for actions (compile/linking) unless the action is explicitly marked as incompatible with remote execution. If turned on, the build will hang if there are no remote agents.";

        private static readonly string _xmlConfigFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Unreal Engine",
            "UnrealBuildTool",
            "BuildConfiguration.xml");

        public Task<bool> GetValueAsync(CancellationToken cancellationToken)
        {
            if (!File.Exists(_xmlConfigFilePath))
            {
                return Task.FromResult(false);
            }

            var document = new XmlDocument();
            document.Load(_xmlConfigFilePath);

            var forceBuildAllRemote = _configHelper.GetValue(document, ["Configuration", "UnrealBuildAccelerator", "bForceBuildAllRemote"]);

            return Task.FromResult(forceBuildAllRemote != "false");
        }

        public Task SetValueAsync(bool value, CancellationToken cancellationToken)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_xmlConfigFilePath)!);

            var document = new XmlDocument();
            if (File.Exists(_xmlConfigFilePath))
            {
                document.Load(_xmlConfigFilePath);
            }
            else
            {
                document.AppendChild(document.CreateXmlDeclaration("1.0", "utf-8", null));
            }

            if (value)
            {
                _configHelper.SetValue(document, ["Configuration", "UnrealBuildAccelerator", "bForceBuildAllRemote"], "true");
                _configHelper.SetValue(document, ["Configuration", "UnrealBuildAccelerator", "bLinkRemote"], "true");
            }
            else
            {
                _configHelper.SetValue(document, ["Configuration", "UnrealBuildAccelerator", "bForceBuildAllRemote"], "false");
                _configHelper.SetValue(document, ["Configuration", "UnrealBuildAccelerator", "bLinkRemote"], "false");
            }

            document.Save(_xmlConfigFilePath);

            return Task.CompletedTask;
        }
    }
}
