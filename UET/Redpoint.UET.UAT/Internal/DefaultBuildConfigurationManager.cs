namespace Redpoint.UET.UAT.Internal
{
    using Microsoft.Extensions.Logging;
    using System;
    using System.Threading.Tasks;

    internal class DefaultBuildConfigurationManager : IBuildConfigurationManager
    {
        private readonly ILogger<DefaultBuildConfigurationManager> _logger;
        private readonly Mutex _buildConfigurationMutex;

        public DefaultBuildConfigurationManager(ILogger<DefaultBuildConfigurationManager> logger)
        {
            _logger = logger;
            _buildConfigurationMutex = new Mutex(false, "UEB_BuildConfiguration.xml");
        }

        public Task<bool> PushBuildConfiguration()
        {
            _buildConfigurationMutex.WaitOne();
            try
            {
                var stackCount = 0;
                var stackCountFilePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Unreal Engine",
                    "UnrealBuildTool",
                    "BuildConfiguration.stackcount");
                if (File.Exists(stackCountFilePath))
                {
                    var stackCountString = File.ReadAllText(stackCountFilePath).Trim();
                    try
                    {
                        stackCount = int.Parse(stackCountString);
                    }
                    catch
                    {
                        _logger.LogWarning($"Unable to parse existing stack count value of '{stackCountString}', defaulting to 0.");
                        stackCount = 0;
                    }
                }
                if (stackCount == 0)
                {
                    // We are the first build, write BuildConfiguration.xml
                    var xmlConfigFilePath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "Unreal Engine",
                        "UnrealBuildTool",
                        "BuildConfiguration.xml");
                    _logger.LogInformation($"Build configuration is located at: {xmlConfigFilePath}");
                    var xmlConfigDirectoryPath = Path.GetDirectoryName(xmlConfigFilePath)!;
                    if (!Directory.Exists(xmlConfigDirectoryPath))
                    {
                        _logger.LogInformation("Creating required directory for build configuration");
                        Directory.CreateDirectory(xmlConfigDirectoryPath);
                    }
                    if (!File.Exists(xmlConfigFilePath) && !File.Exists($"{xmlConfigFilePath}.backup"))
                    {
                        _logger.LogInformation("Moving existing build configuration file out of the way");
                        File.Move(xmlConfigFilePath, $"{xmlConfigFilePath}.backup");
                    }
                    _logger.LogInformation($"Configured build to maximize core and memory usage by updating: {xmlConfigFilePath}");
                    var processorMultiplier = "2";
                    if (OperatingSystem.IsMacOS())
                    {
                        processorMultiplier = "1";
                    }
                    File.WriteAllText(xmlConfigFilePath, $@"
<?xml version=""1.0"" encoding=""utf-8"" ?>
<Configuration xmlns=""https://www.unrealengine.com/BuildConfiguration"">
    <ParallelExecutor>
        <ProcessorCountMultiplier>{processorMultiplier}</ProcessorCountMultiplier>
        <MemoryPerActionBytes>0</MemoryPerActionBytes>
        <bShowCompilationTimes>true</bShowCompilationTimes>
    </ParallelExecutor>
</Configuration>
".Trim());
                    File.WriteAllText(stackCountFilePath, "1");
                }
                else
                {
                    File.WriteAllText(stackCountFilePath, (stackCount + 1).ToString());
                }
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error while pushing build configuration: {ex.Message}");
                return Task.FromResult(false);
            }
            finally
            {
                _buildConfigurationMutex.ReleaseMutex();
            }
        }

        public Task PopBuildConfiguration()
        {
            _buildConfigurationMutex.WaitOne();
            try
            {
                var stackCount = 0;
                var stackCountFilePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Unreal Engine",
                    "UnrealBuildTool",
                    "BuildConfiguration.stackcount");
                if (File.Exists(stackCountFilePath))
                {
                    var stackCountString = File.ReadAllText(stackCountFilePath).Trim();
                    try
                    {
                        stackCount = int.Parse(stackCountString);
                    }
                    catch
                    {
                        _logger.LogWarning($"Unable to parse existing stack count value of '{stackCountString}', defaulting to 0.");
                        stackCount = 0;
                    }
                }
                if (stackCount == 1)
                {
                    // We are the last build, restore BuildConfiguration.xml
                    var xmlConfigFilePath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "Unreal Engine",
                        "UnrealBuildTool",
                        "BuildConfiguration.xml");
                    if (File.Exists(xmlConfigFilePath))
                    {
                        File.Delete(xmlConfigFilePath);
                    }
                    if (File.Exists($"{xmlConfigFilePath}.backup"))
                    {
                        _logger.LogInformation("Moved back existing BuildConfiguration.xml");
                        File.Move($"{xmlConfigFilePath}.backup", xmlConfigFilePath);
                    }
                    File.WriteAllText(stackCountFilePath, "0");
                }
                else
                {
                    File.WriteAllText(stackCountFilePath, (stackCount - 1).ToString());
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error while popping build configuration: {ex.Message}");
            }
            finally
            {
                _buildConfigurationMutex.ReleaseMutex();
            }
            return Task.CompletedTask;
        }
    }
}
