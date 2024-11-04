namespace Redpoint.Uet.BuildPipeline.BuildGraph
{
    public readonly record struct RepositoryRoot
    {
        public string BaseCodePath { get; init; }

        public string PlatformCodePath { get; init; }

        public string OutputPath =>
            !string.IsNullOrWhiteSpace(PlatformCodePath)
                ? PlatformCodePath
                : BaseCodePath;
    }
}
