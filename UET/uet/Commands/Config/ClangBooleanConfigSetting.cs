namespace UET.Commands.Config
{
    using System;
    using System.Threading.Tasks;
    using System.Xml;

    internal sealed class ClangBooleanConfigSetting : IBooleanConfigSetting
    {
        private readonly IXmlConfigHelper _configHelper;

        public ClangBooleanConfigSetting(IXmlConfigHelper configHelper)
        {
            _configHelper = configHelper;
        }

        public string Name => "clang";

        public string Description => "When enabled, the Unreal Engine build process will use Clang instead of MSVC to build the Windows platform.";

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

            var selectedCompiler = _configHelper.GetValue(document, ["Configuration", "WindowsPlatform", "Compiler"]);

            return Task.FromResult(selectedCompiler == "Clang");
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
                _configHelper.SetValue(document, ["Configuration", "WindowsPlatform", "Compiler"], "Clang");
            }
            else
            {
                _configHelper.DeleteValue(document, ["Configuration", "WindowsPlatform", "Compiler"]);
            }

            document.Save(_xmlConfigFilePath);

            return Task.CompletedTask;
        }
    }
}
