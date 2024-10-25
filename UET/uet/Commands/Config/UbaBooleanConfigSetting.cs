namespace UET.Commands.Config
{
    using System;
    using System.Threading.Tasks;
    using System.Xml;

    internal sealed class UbaBooleanConfigSetting : IBooleanConfigSetting
    {
        private readonly IXmlConfigHelper _configHelper;

        public UbaBooleanConfigSetting(IXmlConfigHelper configHelper)
        {
            _configHelper = configHelper;
        }

        public string Name => "uba";

        public string Description => "When enabled, the Unreal Engine build process will use the Unreal Build Accelerator.";

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

            var selectedCompiler = _configHelper.GetValue(document, ["Configuration", "BuildConfiguration", "bAllowUBAExecutor"]);

            return Task.FromResult(selectedCompiler != "false");
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
                _configHelper.SetValue(document, ["Configuration", "BuildConfiguration", "bAllowUBAExecutor"], "true");
                _configHelper.SetValue(document, ["Configuration", "BuildConfiguration", "bAllowUBALocalExecutor"], "true");
            }
            else
            {
                _configHelper.SetValue(document, ["Configuration", "BuildConfiguration", "bAllowUBAExecutor"], "false");
                _configHelper.SetValue(document, ["Configuration", "BuildConfiguration", "bAllowUBALocalExecutor"], "false");
            }

            document.Save(_xmlConfigFilePath);

            return Task.CompletedTask;
        }
    }
}
