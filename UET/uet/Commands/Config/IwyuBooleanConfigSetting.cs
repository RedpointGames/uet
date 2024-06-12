namespace UET.Commands.Config
{
    using System;
    using System.Threading.Tasks;
    using System.Xml;

    internal sealed class IwyuBooleanConfigSetting : IBooleanConfigSetting
    {
        private readonly IXmlConfigHelper _configHelper;

        public IwyuBooleanConfigSetting(IXmlConfigHelper configHelper)
        {
            _configHelper = configHelper;
        }

        public string Name => "iwyu";

        public string Description => "When enabled, the Unreal Engine build process will build each C++ file individually, without unifying build inputs. The build will take much longer, but it will guarantee that all of the include paths in each file are correct.";

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

            var bUseUnityBuild = _configHelper.GetValue(document, ["Configuration", "BuildConfiguration", "bUseUnityBuild"]);
            var bUseSharedPCHs = _configHelper.GetValue(document, ["Configuration", "BuildConfiguration", "bUseSharedPCHs"]);
            var bUsePCHFiles = _configHelper.GetValue(document, ["Configuration", "BuildConfiguration", "bUsePCHFiles"]);

            return Task.FromResult(bUseUnityBuild == "false" && bUseSharedPCHs == "false" && bUsePCHFiles == "false");
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
                _configHelper.SetValue(document, ["Configuration", "BuildConfiguration", "bUseUnityBuild"], "false");
                _configHelper.SetValue(document, ["Configuration", "BuildConfiguration", "bUseSharedPCHs"], "false");
                _configHelper.SetValue(document, ["Configuration", "BuildConfiguration", "bUsePCHFiles"], "false");
            }
            else
            {
                _configHelper.DeleteValue(document, ["Configuration", "BuildConfiguration", "bUseUnityBuild"]);
                _configHelper.DeleteValue(document, ["Configuration", "BuildConfiguration", "bUseSharedPCHs"]);
                _configHelper.DeleteValue(document, ["Configuration", "BuildConfiguration", "bUsePCHFiles"]);
            }

            document.Save(_xmlConfigFilePath);

            return Task.CompletedTask;
        }
    }
}
