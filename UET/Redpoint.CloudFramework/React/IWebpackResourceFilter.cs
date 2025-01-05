namespace Redpoint.CloudFramework.React
{
    public interface IWebpackResourceFilter
    {
        bool ShouldIncludeResource(string path);
    }
}
