namespace Redpoint.Uet.BuildPipeline.BuildGraph
{
    public readonly record struct BuildGraphArgumentContext
    {
        public required RepositoryRoot RepositoryRoot { get; init; }
        public required string UetPath { get; init; }
        public required string EnginePath { get; init; }
        public required string SharedStoragePath { get; init; }
        public required string ArtifactExportPath { get; init; }
    }
}
