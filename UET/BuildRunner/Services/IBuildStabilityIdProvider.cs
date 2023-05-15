namespace BuildRunner.Services
{
    internal interface IBuildStabilityIdProvider
    {
        string GetBuildStabilityId(string? workingDirectory, string? engine, string suffix);
    }
}
