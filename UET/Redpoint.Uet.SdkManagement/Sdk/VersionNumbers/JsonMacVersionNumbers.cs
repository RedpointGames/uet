namespace Redpoint.Uet.SdkManagement.Sdk.VersionNumbers
{
    using Microsoft.Extensions.Logging;
    using System.Text.Json;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    internal class JsonMacVersionNumbers : IMacVersionNumbers
    {
        private readonly ILogger<JsonMacVersionNumbers> _logger;

        private bool _hasEmittedLogs = false;

        public JsonMacVersionNumbers(
            ILogger<JsonMacVersionNumbers> logger)
        {
            _logger = logger;
        }

        public int Priority => 200;

        public bool CanUse(string unrealEnginePath)
        {
            return Path.Exists(Path.Combine(
                unrealEnginePath,
                "Engine",
                "Config",
                "Apple",
                "Apple_SDK.json"));
        }

        public async Task<string> GetXcodeVersion(string unrealEnginePath)
        {
            var json = await File.ReadAllTextAsync(Path.Combine(
                unrealEnginePath,
                "Engine",
                "Config",
                "Apple",
                "Apple_SDK.json")).ConfigureAwait(false);
            var dictionary = JsonSerializer.Deserialize(
                json,
                new JsonConfigJsonSerializerContext(new JsonSerializerOptions
                {
                    AllowTrailingCommas = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                }).DictionaryStringJsonElement);
            if (dictionary == null)
            {
                throw new InvalidOperationException("Unable to read Apple SDK versions from Apple_SDK.json!");
            }

            var appleXcodeStoragePath = Environment.GetEnvironmentVariable("UET_APPLE_XCODE_STORAGE_PATH");
            if (string.IsNullOrWhiteSpace(appleXcodeStoragePath))
            {
                if (!_hasEmittedLogs)
                {
                    _logger.LogWarning("UET_APPLE_XCODE_STORAGE_PATH is not set, so we can not determine the highest available Xcode version to use from 'MaxVersion' and 'MinVersion' in Apple_SDK.json. The value of 'MainVersion' will always be used.");
                }
            }
            if (dictionary.TryGetValue("MaxVersion", out var maxVersion) &&
                dictionary.TryGetValue("MinVersion", out var minVersion) &&
                !string.IsNullOrWhiteSpace(appleXcodeStoragePath))
            {
                var regex = new Regex("^(?<major>[0-9]+)\\.(?<minor>[0-9]+)\\.");
                var maxMatch = regex.Match(maxVersion.ToString());
                var minMatch = regex.Match(minVersion.ToString());
                if (maxMatch.Success &&
                    minMatch.Success &&
                    int.TryParse(maxMatch.Groups["major"].Value, out var major) &&
                    int.TryParse(maxMatch.Groups["minor"].Value, out var minor) &&
                    int.TryParse(minMatch.Groups["major"].Value, out var minMajor) &&
                    int.TryParse(minMatch.Groups["minor"].Value, out var minMinor))
                {
                    if (!_hasEmittedLogs)
                    {
                        _logger.LogInformation($"The maximum Apple SDK version permitted for this version of Unreal Engine is: Xcode {major}.{minor}");
                        _logger.LogInformation($"The minimum Apple SDK version permitted for this version of Unreal Engine is: Xcode {minMajor}.{minMinor}");
                    }

                    // The version number for MaxVersion can be ahead of the released Xcode version. For example, it could be 16.9 while
                    // the latest release is 16.3.
                    //
                    // To figure out the actual version, check the files in UET_APPLE_XCODE_STORAGE_PATH and see what is available, decrementing
                    // the version number until we find a file that exists.
                    var foundVersion = false;
                    do
                    {
                        var xipSourcePath = Path.Combine(appleXcodeStoragePath, $"Xcode_{major}.{minor}.xip");
                        if (File.Exists(xipSourcePath))
                        {
                            if (!_hasEmittedLogs)
                            {
                                _logger.LogInformation($"Detected that the Apple SDK to use for this version of Unreal Engine is: Xcode {major}.{minor}");
                            }
                            foundVersion = true;
                            break;
                        }
                        minor--;
                        if (minor == -1)
                        {
                            // No idea what the maximum minor version can be, so just overestimate.
                            minor = 20;
                            major--;
                        }
                    }
                    while ((major == minMajor && minor >= minMinor) || major > minMajor);

                    if (foundVersion)
                    {
                        _hasEmittedLogs = true;
                        return $"{major}.{minor}";
                    }
                }
            }

            if (dictionary.TryGetValue("MainVersion", out var mainVersion))
            {
                if (!_hasEmittedLogs)
                {
                    _logger.LogInformation($"Detected that the Apple SDK to use from 'MainVersion' for this version of Unreal Engine is: Xcode {mainVersion.ToString()}");
                }

                _hasEmittedLogs = true;
                return mainVersion.ToString();
            }

            throw new InvalidOperationException("Unable to read Apple SDK versions from either 'MaxVersion' or 'MainVersion' in Apple_SDK.json!");
        }
    }
}
