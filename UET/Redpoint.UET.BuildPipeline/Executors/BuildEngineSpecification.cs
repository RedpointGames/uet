namespace Redpoint.UET.BuildPipeline.Executors
{
    public class BuildEngineSpecification
    {
        internal string? _enginePath { get; private set; }
        internal string? _uefsPackageTag { get; private set; }
        internal string? _uefsGitUrl { get; private set; }
        internal string? _uefsGitCommit { get; private set; }
        internal string[]? _uefsGitFolders { get; private set; }
        internal bool _permitConcurrentBuilds { get; private set; } = false;

        public static BuildEngineSpecification ForPath(string path)
        {
            return new BuildEngineSpecification
            {
                _enginePath = path,
                _permitConcurrentBuilds = false,
            };
        }

        public static BuildEngineSpecification ForUEFSPackageTag(string uefsPackageTag)
        {
            return new BuildEngineSpecification
            {
                _uefsPackageTag = uefsPackageTag,
                _permitConcurrentBuilds = true,
            };
        }

        public static BuildEngineSpecification ForUEFSGitCommit(string uefsGitUrl, string uefsGitCommit, string[]? uefsGitAdditionalFolders = null)
        {
            return new BuildEngineSpecification
            {
                _uefsGitUrl = uefsGitUrl,
                _uefsGitCommit = uefsGitCommit,
                _uefsGitFolders = uefsGitAdditionalFolders ?? new string[0],
                _permitConcurrentBuilds = true,
            };
        }
    }
}
