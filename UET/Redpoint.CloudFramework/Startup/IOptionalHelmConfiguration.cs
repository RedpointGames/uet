namespace Redpoint.CloudFramework.Startup
{
    public interface IOptionalHelmConfiguration
    {
        HelmConfiguration? GetHelmConfig();
    }
}
