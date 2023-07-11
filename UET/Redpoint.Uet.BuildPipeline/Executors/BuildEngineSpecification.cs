namespace Redpoint.Uet.BuildPipeline.Executors
{
    public class BuildEngineSpecification
    {
        internal string? _engineVersion { get; private set; }
        internal string? _enginePath { get; private set; }
        internal string? _uefsPackageTag { get; private set; }
        internal string? _gitUrl { get; private set; }
        internal string? _gitCommit { get; private set; }
        internal string[]? _gitConsoleZips { get; private set; }
        internal bool _isEngineBuild { get; private set; }
        public bool PermitConcurrentBuilds { get; private set; } = false;

        public static BuildEngineSpecification ForVersionWithPath(string version, string localPath)
        {
            return new BuildEngineSpecification
            {
                _engineVersion = version,
                _enginePath = localPath,
                PermitConcurrentBuilds = false,
            };
        }

        public static BuildEngineSpecification ForAbsolutePath(string path)
        {
            return new BuildEngineSpecification
            {
                _enginePath = path,
                PermitConcurrentBuilds = false,
            };
        }

        public static BuildEngineSpecification ForUEFSPackageTag(string uefsPackageTag)
        {
            return new BuildEngineSpecification
            {
                _uefsPackageTag = uefsPackageTag,
                PermitConcurrentBuilds = true,
            };
        }

        public static BuildEngineSpecification ForGitCommitWithZips(string uefsGitUrl, string uefsGitCommit, string[]? uefsGitConsoleZips = null, bool isEngineBuild = false)
        {
            return new BuildEngineSpecification
            {
                _gitUrl = uefsGitUrl,
                _gitCommit = uefsGitCommit,
                _gitConsoleZips = uefsGitConsoleZips ?? new string[0],
                _isEngineBuild = isEngineBuild,
                PermitConcurrentBuilds = true,
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
                return $"git:{_gitCommit}@{_gitUrl}{string.Join("", (_gitConsoleZips ?? Array.Empty<string>()).Select(x => $",z:{x}"))}";
            }
            else
            {
                throw new NotSupportedException();
            }
        }
    }
}
