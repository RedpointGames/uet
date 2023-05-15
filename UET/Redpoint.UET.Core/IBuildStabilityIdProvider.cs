namespace Redpoint.UET.Core
{
    public interface IBuildStabilityIdProvider
    {
        string GetBuildStabilityId(string? workingDirectory, string? engine, string suffix);
    }
}
