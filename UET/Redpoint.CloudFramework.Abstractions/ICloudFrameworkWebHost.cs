namespace Redpoint.CloudFramework.Abstractions
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1040:Avoid empty interfaces", Justification = "This is intentionally an opaque interface to shield consumers from changes to the IHost/IWebHost types in future.")]
    public interface ICloudFrameworkWebHost
    {
        IServiceProvider Services { get; }
    }
}
