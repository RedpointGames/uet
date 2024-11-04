namespace Redpoint.Uet.BuildPipeline.MultiWorkspace
{
    public readonly record struct MultiWorkspaceDescriptor
    {
        public required readonly InterpolatedString GitUrl { get; init; }

        public required readonly InterpolatedString GitRef { get; init; }

        public required readonly string LocalPath { get; init; }
    }
}
