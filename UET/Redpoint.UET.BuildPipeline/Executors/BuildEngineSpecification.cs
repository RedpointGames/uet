namespace Redpoint.UET.BuildPipeline.Executors
{
    public class BuildEngineSpecification
    {
        internal string? _engineVersion { get; private set; }
        internal string? _enginePath { get; private set; }
        internal string? _uefsPackageTag { get; private set; }
        internal string? _uefsGitUrl { get; private set; }
        internal string? _uefsGitCommit { get; private set; }
        internal string[]? _uefsGitFolders { get; private set; }
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

        public static BuildEngineSpecification ForUEFSGitCommit(string uefsGitUrl, string uefsGitCommit, string[]? uefsGitAdditionalFolders = null)
        {
            return new BuildEngineSpecification
            {
                _uefsGitUrl = uefsGitUrl,
                _uefsGitCommit = uefsGitCommit,
                _uefsGitFolders = uefsGitAdditionalFolders ?? new string[0],
                PermitConcurrentBuilds = true,
            };
        }

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
            else
            {
                throw new NotSupportedException();
            }
        }
    }
}
