namespace Redpoint.Uet.SdkManagement.Sdk.VersionNumbers
{
    using Microsoft.Extensions.Logging;
    using Redpoint.Uet.SdkManagement.Sdk.GenericPlatform;
    using System.Runtime.Versioning;
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

        private static GenericPlatformVersion? ParseVersionFromDictionary(Dictionary<string, JsonElement> dictionary, string key)
        {
            if (dictionary.TryGetValue(key, out var version) &&
                version.ValueKind == JsonValueKind.String)
            {
                var versionString = version.GetString();
                if (versionString != null)
                {
                    return GenericPlatformVersion.Parse(versionString);
                }
            }
            return null;
        }

        private static GenericPlatformVersion? ClampVersion(GenericPlatformVersion? version, int major, int minor)
        {
            if (version != null)
            {
                if (version.Major < major || (version.Major == major && version.Minor < minor))
                {
                    return GenericPlatformVersion.Parse($"{major}.{minor}");
                }
            }
            return version;
        }

        internal static GenericPlatformVersion? GetBestAvailableVersionFromAvailableXcodes(
            GenericPlatformVersion? mainVersion,
            GenericPlatformVersion? minVersion,
            GenericPlatformVersion? maxVersion,
            IEnumerable<FileInfo> availableXcodes,
            ILogger? logger = null)
        {
            return GetBestAvailableVersionFromAvailableXcodes(
                mainVersion,
                minVersion,
                maxVersion,
                availableXcodes.Select(x => x.Name),
                logger);
        }

        internal static GenericPlatformVersion? GetBestAvailableVersionFromAvailableXcodes(
            GenericPlatformVersion? mainVersion,
            GenericPlatformVersion? minVersion,
            GenericPlatformVersion? maxVersion,
            IEnumerable<string> availableXcodes,
            ILogger? logger = null)
        {
            var candidateVersionRegex = new Regex("^Xcode_(?<version>.*)\\.xip$");
            var absoluteMaximumVersion = GenericPlatformVersion.Parse("999")!;
            GenericPlatformVersion? selectedVersion = null;
            long selectedVersionDistance = long.MaxValue;
            long candidateCount = 0;
            foreach (var availableXcode in availableXcodes)
            {
                var candidateVersionMatch = candidateVersionRegex.Match(availableXcode);
                if (candidateVersionMatch.Success)
                {
                    var candidateVersion = GenericPlatformVersion.Parse(candidateVersionMatch.Groups["version"].Value);
                    if (candidateVersion != null)
                    {
                        candidateCount++;
                        var allowed = true;
                        if (minVersion != null && candidateVersion - minVersion < 0)
                        {
                            if (allowed)
                            {
                                logger?.LogInformation($"Candidate Xcode version '{candidateVersion}' will not be selected because it is lower than the minimum version '{minVersion}'.");
                            }
                            allowed = false;
                        }
                        if (maxVersion != null && maxVersion - candidateVersion < 0)
                        {
                            if (allowed)
                            {
                                logger?.LogInformation($"Candidate Xcode version '{candidateVersion}' will not be selected because it is higher than the maximum version '{maxVersion}'.");
                            }
                            allowed = false;
                        }
                        if (allowed)
                        {
                            long distance;
                            if (mainVersion != null)
                            {
                                // How close is it to the main version?
                                distance = candidateVersion - mainVersion;
                                if (distance > 0)
                                {
                                    // The candidate version is higher than the main version; prefer
                                    // a higher version one minor up than a lower version one minor down, in case the engine relies on compiler features introduced in the main version that aren't actually compatible with min version.
                                    distance /= 2;
                                }
                                distance = Math.Abs(distance);
                            }
                            else if (maxVersion != null)
                            {
                                // How close is it in the max version.
                                distance = Math.Abs(maxVersion - candidateVersion);
                            }
                            else
                            {
                                // How high is the version.
                                distance = Math.Abs(absoluteMaximumVersion - candidateVersion);
                            }
                            if (selectedVersion == null || distance < selectedVersionDistance)
                            {
                                selectedVersion = candidateVersion;
                                selectedVersionDistance = distance;
                            }
                        }
                    }
                }
            }

            if (selectedVersion != null)
            {
                logger?.LogInformation($"Candidate Xcode version '{selectedVersion}' was selected as the best available Xcode version.");
                return selectedVersion;
            }
            else
            {
                logger?.LogWarning($"Unable to find a candidate Xcode version that met the constraints of MinVersion={minVersion},MaxVersion={maxVersion},MainVersion={mainVersion} with {candidateCount} candidates available.");
                return null;
            }
        }

        [SupportedOSPlatform("macos")]
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

            var mainVersion = ParseVersionFromDictionary(dictionary, "MainVersion");
            var minVersion = ParseVersionFromDictionary(dictionary, "MinVersion");
            var maxVersion = ParseVersionFromDictionary(dictionary, "MaxVersion");

            // Fab compiles with 15.4 even for engines that specify 15.2 as their MainVersion, at least
            // for Unreal Engine 5.5. I am waiting on Fab support to give me a full list of engine version -> Xcode
            // versions that they use, given that it doesn't seem to be based on the SDK version files
            // in the engine itself.
            mainVersion = ClampVersion(mainVersion, 15, 4);
            minVersion = ClampVersion(mainVersion, 15, 4);

            if (!string.IsNullOrWhiteSpace(appleXcodeStoragePath))
            {
                var selectedVersion = GetBestAvailableVersionFromAvailableXcodes(
                    mainVersion,
                    minVersion,
                    maxVersion,
                    new DirectoryInfo(appleXcodeStoragePath).GetFiles("Xcode_*.xip"),
                    _hasEmittedLogs ? null : _logger);
                if (selectedVersion != null)
                {
                    if (!_hasEmittedLogs)
                    {
                        _logger.LogInformation($"Detected that the Apple SDK to use from 'MainVersion' for this version of Unreal Engine is: Xcode {selectedVersion}");
                    }

                    _hasEmittedLogs = true;
                    return $"{selectedVersion.Major}.{selectedVersion.Minor}";
                }
            }

            if (mainVersion != null)
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

        [SupportedOSPlatform("windows")]
        public async Task<string> GetITunesVersion(string unrealEnginePath)
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

            if (dictionary.TryGetValue("MinVersion_Win64", out var minVersion))
            {
                return minVersion.GetString() ?? "Unspecified";
            }
            else
            {
                return "Unspecified";
            }
        }
    }
}
