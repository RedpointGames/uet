namespace Redpoint.Windows.HandleManagement
{
    using System.Runtime.Versioning;

    /// <summary>
    /// The common interface for <see cref="FileNativeHandle"/> and <see cref="RawNativeHandle"/>.
    /// </summary>
    [SupportedOSPlatform("windows6.2")]
    public interface INativeHandle
    {
        /// <summary>
        /// The process that holds this handle.
        /// </summary>
        int ProcessId { get; }
    }
}
