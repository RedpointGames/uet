namespace UET.Commands.Config
{
    using System;
    using System.Threading.Tasks;
    using System.Xml;

    internal sealed class MaxCpuBooleanConfigSetting : IBooleanConfigSetting
    {
        public string Name => "maxcpu";

        public string Description => "When enabled, the Unreal Engine build process will use all CPU cores (regardless of the available memory) and will treat each hyperthread as a CPU (instead of counting physical CPU cores). Expect the performance of background tasks to suffer when Unreal Engine is compiling with this option on. You may also need to increase your paging file size if you see compiler errors relating to available memory.";

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

            var bAllCores = document.SelectSingleNode("/Configuration/BuildConfiguration/bAllCores")?.InnerText;
            var MemoryPerActionBytes = document.SelectSingleNode("/Configuration/ParallelExecutor/MemoryPerActionBytes")?.InnerText;

            return Task.FromResult(bAllCores == "true" && MemoryPerActionBytes == "0");
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

            var buildConfiguration = configuration.SelectSingleNode("/BuildConfiguration");
            if (buildConfiguration == null)
            {
                buildConfiguration = document.CreateElement("BuildConfiguration", _ns);
                configuration.AppendChild(buildConfiguration);
            }

            {
                var element = buildConfiguration.SelectSingleNode("/bAllCores");
                if (value)
                {
                    if (element == null)
                    {
                        element = document.CreateElement("bAllCores", _ns);
                        buildConfiguration.AppendChild(element);
                    }
                    element.InnerText = "true";
                }
                else if (element != null)
                {
                    element.ParentNode!.RemoveChild(element);
                }
            }

            var parallelExecutor = configuration.SelectSingleNode("/ParallelExecutor");
            if (parallelExecutor == null)
            {
                parallelExecutor = document.CreateElement("ParallelExecutor", _ns);
                configuration.AppendChild(parallelExecutor);
            }

            {
                var element = parallelExecutor.SelectSingleNode("/MemoryPerActionBytes");
                if (value)
                {
                    if (element == null)
                    {
                        element = document.CreateElement("MemoryPerActionBytes", _ns);
                        parallelExecutor.AppendChild(element);
                    }
                    element.InnerText = "0";
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
