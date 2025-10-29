namespace Redpoint.Uet.BuildPipeline.Executors
{
    using System.Collections.Specialized;
    using System.Diagnostics.CodeAnalysis;
    using System.Web;

    public enum BuildEngineSpecificationEngineBuildType
    {
        None,

        ExternalSource,

        CurrentWorkspace,
    }

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
        internal string? _remoteZfs { get; private set; }
        internal bool _currentWorkspace { get; private set; }
        internal NameValueCollection? _queryString { get; private set; }
        public BuildEngineSpecificationEngineBuildType EngineBuildType { get; private set; } = BuildEngineSpecificationEngineBuildType.None;

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

        public static BuildEngineSpecification ForRemoteZfs(string remoteZfs)
        {
            return new BuildEngineSpecification
            {
                _remoteZfs = remoteZfs,
            };
        }

        [SuppressMessage("Design", "CA1054:URI-like parameters should not be strings", Justification = "Git URLs are not compatible with the Uri object.")]
        public static BuildEngineSpecification ForGitCommitWithZips(
            string uefsGitUrl,
            string uefsGitCommit,
            string[]? uefsGitConsoleZips = null,
            bool isEngineBuild = false,
            string? windowsSharedGitCachePath = null,
            string? macSharedGitCachePath = null,
            NameValueCollection? queryString = null)
        {
            return new BuildEngineSpecification
            {
                _gitUrl = uefsGitUrl,
                _gitCommit = uefsGitCommit,
                _gitConsoleZips = uefsGitConsoleZips ?? Array.Empty<string>(),
                _gitSharedWindowsCachePath = windowsSharedGitCachePath,
                _gitSharedMacCachePath = macSharedGitCachePath,
                _queryString = queryString,
                EngineBuildType = isEngineBuild
                    ? BuildEngineSpecificationEngineBuildType.ExternalSource
                    : BuildEngineSpecificationEngineBuildType.None,
            };
        }

        public static BuildEngineSpecification ForEngineInCurrentWorkspace()
        {
            return new BuildEngineSpecification
            {
                _currentWorkspace = true,
                EngineBuildType = BuildEngineSpecificationEngineBuildType.CurrentWorkspace,
            };
        }

        /// <remarks>
        /// These values are parsed in `UET.Commands.EngineSpec.EngineSpec.TryParseEngine`.
        /// </remarks>
        public string ToReparsableString()
        {
            if (_currentWorkspace)
            {
                return $"self:true";
            }
            else if (!string.IsNullOrWhiteSpace(_uefsPackageTag))
            {
                return $"uefs:{_uefsPackageTag}";
            }
            else if (!string.IsNullOrWhiteSpace(_sesNetworkShare))
            {
                return $"ses:{_sesNetworkShare}";
            }
            else if (!string.IsNullOrWhiteSpace(_remoteZfs))
            {
                return $"rzfs:{_remoteZfs}";
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
                if (options.Count > 0)
                {
                    if (_queryString == null)
                    {
                        _queryString = [];
                    }
                    if (_queryString["config"] == null)
                    {
                        // Only set 'config' if it's not already set, since the options
                        // will have originally come from the 'config' key if it exists.
                        _queryString["config"] = string.Join("", options).TrimStart(',');
                    }
                }

                var queryString = (_queryString == null || _queryString.Count == 0)
                    ? string.Empty
                    : ("?" + string.Join("&",
                        _queryString.AllKeys.Select(a => HttpUtility.UrlEncode(a) + "=" + HttpUtility.UrlEncode(_queryString[a]))));
                return $"git:{_gitCommit}@{_gitUrl}{queryString}";
            }
            else
            {
                throw new NotSupportedException();
            }
        }

        public bool IsNonConcurrent
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(_engineVersion))
                {
                    return true;
                }

                if (!string.IsNullOrWhiteSpace(_enginePath))
                {
                    return true;
                }

                if (!string.IsNullOrWhiteSpace(_gitUrl))
                {
                    if (_queryString != null &&
                        _queryString["concurrent"] == "false")
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public bool NoUefsWriteScratchReuse { get; set; }
    }
}
