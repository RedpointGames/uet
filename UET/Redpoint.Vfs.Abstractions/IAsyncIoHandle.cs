namespace Redpoint.Vfs.Abstractions
{
    using Microsoft.Win32.SafeHandles;
    using System.Runtime.Versioning;

    [SupportedOSPlatform("windows6.2")]
    public interface IAsyncIoHandle
    {
        SafeFileHandle SafeFileHandle { get; }
    }
}
