namespace UET.Commands.Config
{
    using System;
    using System.Threading.Tasks;
    using System.Xml;

    internal sealed class ClangBooleanConfigSetting : IBooleanConfigSetting
    {
        public string Name => "clang";

        public string Description => "When enabled, the Unreal Engine build process will use Clang instead of MSVC to build the Windows platform.";

        private static readonly string _xmlConfigFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Unreal Engine",
            "UnrealBuildTool",
            "BuildConfiguration.xml");

        private const string _ns = "https://www.unrealengine.com/BuildConfiguration";

        public Task<bool> GetValueAsync(CancellationToken cancellationToken)
        {
            if (!File.Exists(_xmlConfigFilePath))
            {
                return Task.FromResult(false);
            }

            var document = new XmlDocument();
            document.Load(_xmlConfigFilePath);

            var SelectedCompiler = document.SelectSingleNode("/Configuration/WindowsPlatform/Compiler")?.InnerText;

            return Task.FromResult(SelectedCompiler == "Clang");
        }

        public Task SetValueAsync(bool value, CancellationToken cancellationToken)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_xmlConfigFilePath)!);

            var document = new XmlDocument();
            if (!File.Exists(_xmlConfigFilePath))
            {
                document.Load(_xmlConfigFilePath);
            }
            else
            {
                document.AppendChild(document.CreateXmlDeclaration("1.0", "utf-8", null));
            }

            var configuration = document.SelectSingleNode("/Configuration");
            if (configuration == null)
            {
                configuration = document.CreateElement("Configuration", _ns);
                document.AppendChild(configuration);
            }

            var windowsPlatform = configuration.SelectSingleNode("/WindowsPlatform");
            if (windowsPlatform == null)
            {
                windowsPlatform = document.CreateElement("WindowsPlatform", _ns);
                configuration.AppendChild(windowsPlatform);
            }

            {
                var element = windowsPlatform.SelectSingleNode("/Compiler");
                if (value)
                {
                    if (element == null)
                    {
                        element = document.CreateElement("Compiler", _ns);
                        windowsPlatform.AppendChild(element);
                    }
                    element.InnerText = "Clang";
                }
                else if (element != null)
                {
                    element.ParentNode!.RemoveChild(element);
                }
            }

            document.Save(_xmlConfigFilePath);

            return Task.CompletedTask;
        }
    }
}
