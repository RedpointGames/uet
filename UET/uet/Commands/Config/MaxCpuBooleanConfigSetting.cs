namespace UET.Commands.Config
{
    using System;
    using System.Threading.Tasks;
    using System.Xml;

    internal sealed class MaxCpuBooleanConfigSetting : IBooleanConfigSetting
    {
        private readonly IXmlConfigHelper _configHelper;

        public MaxCpuBooleanConfigSetting(IXmlConfigHelper configHelper)
        {
            _configHelper = configHelper;
        }

        public string Name => "maxcpu";

        public string Description => "When enabled, the Unreal Engine build process will use all CPU cores (regardless of the available memory) and will treat each hyperthread as a CPU (instead of counting physical CPU cores). Expect the performance of background tasks to suffer when Unreal Engine is compiling with this option on. You may also need to increase your paging file size if you see compiler errors relating to available memory.";

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

            var bAllCores = _configHelper.GetValue(document, ["Configuration", "BuildConfiguration", "bAllCores"]);
            var MemoryPerActionBytes = _configHelper.GetValue(document, ["Configuration", "ParallelExecutor", "MemoryPerActionBytes"]);

            return Task.FromResult(bAllCores == "true" && MemoryPerActionBytes == "0");
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
                _configHelper.SetValue(document, ["Configuration", "BuildConfiguration", "bAllCores"], "true");
                _configHelper.SetValue(document, ["Configuration", "ParallelExecutor", "MemoryPerActionBytes"], "0");
            }
            else
            {
                _configHelper.DeleteValue(document, ["Configuration", "BuildConfiguration", "bAllCores"]);
                _configHelper.DeleteValue(document, ["Configuration", "ParallelExecutor", "MemoryPerActionBytes"]);
            }

            document.Save(_xmlConfigFilePath);

            return Task.CompletedTask;
        }
    }
}
