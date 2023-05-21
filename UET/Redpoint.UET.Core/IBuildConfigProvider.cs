namespace Redpoint.UET.Core
{
    using Redpoint.UET.Configuration;

    [Obsolete]
    public interface IBuildConfigProvider
    {
        BuildConfig GetBuildConfig();
    }
}
