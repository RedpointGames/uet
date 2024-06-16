namespace Redpoint.Uet.BuildPipeline.Executors
{
    using System.Diagnostics.CodeAnalysis;

    public class BuildEngineSpecification
    {
        internal string? _engineVersion { get; private set; }
        internal string? _enginePath { get; private set; }
        internal string? _uefsPackageTag { get; private set; }
        internal string? _gitUrl { get; private set; }
        internal string? _gitCommit { get; private set; }
        internal string[]? _gitConsoleZips { get; private set; }
        internal string? _gitSharedWindowsCachePath { get; private set; }
        internal string? _gitSharedMacCachePath { get; private set; }
        internal string? _sesNetworkShare { get; private set; }
        public bool IsEngineBuild { get; private set; } = false;

        public static BuildEngineSpecification ForVersionWithPath(string version, string localPath)
        {
            return new BuildEngineSpecification
            {
                _engineVersion = version,
                _enginePath = localPath,
            };
        }

        public static BuildEngineSpecification ForAbsolutePath(string path)
        {
            return new BuildEngineSpecification
            {
                _enginePath = path,
            };
        }

        public static BuildEngineSpecification ForUEFSPackageTag(string uefsPackageTag)
        {
            return new BuildEngineSpecification
            {
                _uefsPackageTag = uefsPackageTag,
            };
        }

        public static BuildEngineSpecification ForSESNetworkShare(string sesNetworkShare)
        {
            return new BuildEngineSpecification
            {
                _sesNetworkShare = sesNetworkShare,
            };
        }

        [SuppressMessage("Design", "CA1054:URI-like parameters should not be strings", Justification = "Git URLs are not compatible with the Uri object.")]
        public static BuildEngineSpecification ForGitCommitWithZips(
            string uefsGitUrl,
            string uefsGitCommit,
            string[]? uefsGitConsoleZips = null,
            bool isEngineBuild = false,
            string? windowsSharedGitCachePath = null,
            string? macSharedGitCachePath = null)
        {
            return new BuildEngineSpecification
            {
                _gitUrl = uefsGitUrl,
                _gitCommit = uefsGitCommit,
                _gitConsoleZips = uefsGitConsoleZips ?? Array.Empty<string>(),
                _gitSharedWindowsCachePath = windowsSharedGitCachePath,
                _gitSharedMacCachePath = macSharedGitCachePath,
                IsEngineBuild = isEngineBuild,
            };
        }

        /// <remarks>
        /// These values are parsed in <see cref="UET.Commands.EngineSpec.EngineSpec.TryParseEngine"/>.
        /// </remarks>
        public string ToReparsableString()
        {
            if (!string.IsNullOrWhiteSpace(_uefsPackageTag))
            {
                return $"uefs:{_uefsPackageTag}";
            }
            else if (!string.IsNullOrWhiteSpace(_sesNetworkShare))
            {
                return $"ses:{_sesNetworkShare}";
            }
            else if (!string.IsNullOrWhiteSpace(_engineVersion))
            {
                return _engineVersion;
            }
            else if (!string.IsNullOrWhiteSpace(_enginePath))
            {
                return _enginePath;
            }
            else if (!string.IsNullOrWhiteSpace(_gitUrl))
            {
                var options = new List<string>();
                foreach (var z in _gitConsoleZips ?? Array.Empty<string>())
                {
                    options.Add($",z:{z}");
                }
                if (_gitSharedWindowsCachePath != null)
                {
                    options.Add($",wc:{_gitSharedWindowsCachePath}");
                }
                if (_gitSharedMacCachePath != null)
                {
                    options.Add($",mc:{_gitSharedMacCachePath}");
                }
                return $"git:{_gitCommit}@{_gitUrl}{string.Join("", options)}";
            }
            else
            {
                throw new NotSupportedException();
            }
        }
    }
}
