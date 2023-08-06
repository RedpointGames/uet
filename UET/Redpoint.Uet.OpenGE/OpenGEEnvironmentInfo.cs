namespace Redpoint.Uet.OpenGE
{
    public record class OpenGEEnvironmentInfo
    {
        public required bool ShouldUseOpenGE { get; init; }

        public required bool UsingSystemWideDaemon { get; init; }

        public required string PerProcessDispatcherPipeName { get; init; }
    }
}
