namespace BuildRunner.Services
{
    using BuildRunner.Configuration;

    internal interface IBuildConfigProvider
    {
        BuildConfig GetBuildConfig();
    }
}
