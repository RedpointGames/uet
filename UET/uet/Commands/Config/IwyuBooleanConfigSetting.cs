namespace UET.Commands.Config
{
    using System;
    using System.Threading.Tasks;
    using System.Xml;

    internal sealed class IwyuBooleanConfigSetting : IBooleanConfigSetting
    {
        public string Name => "iwyu";

        public string Description => "When enabled, the Unreal Engine build process will build each C++ file individually, without unifying build inputs. The build will take much longer, but it will guarantee that all of the include paths in each file are correct.";

        private static readonly string _xmlConfigFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Unreal Engine",
            "UnrealBuildTool",
            "BuildConfiguration.xml");

        private static readonly string _ns = "https://www.unrealengine.com/BuildConfiguration";

        public Task<bool> GetValueAsync(CancellationToken cancellationToken)
        {
            if (!File.Exists(_xmlConfigFilePath))
            {
                return Task.FromResult(false);
            }

            var document = new XmlDocument();
            document.Load(_xmlConfigFilePath);

            var bUseUnityBuild = document.SelectSingleNode("/Configuration/BuildConfiguration/bUseUnityBuild")?.InnerText;
            var bUseSharedPCHs = document.SelectSingleNode("/Configuration/BuildConfiguration/bUseSharedPCHs")?.InnerText;
            var bUsePCHFiles = document.SelectSingleNode("/Configuration/BuildConfiguration/bUsePCHFiles")?.InnerText;

            return Task.FromResult(bUseUnityBuild == "false" && bUseSharedPCHs == "false" && bUsePCHFiles == "false");
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

            var buildConfiguration = configuration.SelectSingleNode("/Configuration");
            if (buildConfiguration == null)
            {
                buildConfiguration = document.CreateElement("BuildConfiguration", _ns);
                configuration.AppendChild(buildConfiguration);
            }

            {
                var element = buildConfiguration.SelectSingleNode("/bUseUnityBuild");
                if (value)
                {
                    if (element == null)
                    {
                        element = document.CreateElement("bUseUnityBuild", _ns);
                        buildConfiguration.AppendChild(element);
                    }
                    element.InnerText = "false";
                }
                else if (element != null)
                {
                    element.ParentNode!.RemoveChild(element);
                }
            }

            {
                var element = buildConfiguration.SelectSingleNode("/bUseSharedPCHs");
                if (value)
                {
                    if (element == null)
                    {
                        element = document.CreateElement("bUseSharedPCHs", _ns);
                        buildConfiguration.AppendChild(element);
                    }
                    element.InnerText = "false";
                }
                else if (element != null)
                {
                    element.ParentNode!.RemoveChild(element);
                }
            }

            {
                var element = buildConfiguration.SelectSingleNode("/bUsePCHFiles");
                if (value)
                {
                    if (element == null)
                    {
                        element = document.CreateElement("bUsePCHFiles", _ns);
                        buildConfiguration.AppendChild(element);
                    }
                    element.InnerText = "false";
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
