namespace Redpoint.Windows.HandleManagement
{
    using System.Runtime.Versioning;

    [SupportedOSPlatform("windows6.2")]
    internal interface INativeHandleInternal : INativeHandle
    {
        NativeHandles.SYSTEM_HANDLE Handle { get; }
    }
}
