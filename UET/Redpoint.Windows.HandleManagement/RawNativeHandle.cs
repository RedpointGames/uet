namespace Redpoint.Windows.HandleManagement
{
    using System.Runtime.Versioning;

    /// <summary>
    /// Represents a handle to any type of Win32 object. This includes internal
    /// Windows objects, registry entries, files, etc.
    /// </summary>
    /// <remarks>
    /// <see cref="RawNativeHandle"/> objects that represent file handles will not have the file paths translated to normal file paths. Instead the object paths will start with <code>\Device\&lt;DeviceName&gt;</code> because this is the format that Windows uses internally.
    /// </remarks>
    [SupportedOSPlatform("windows6.2")]
    public class RawNativeHandle : INativeHandleInternal
    {
        internal readonly NativeHandles.SYSTEM_HANDLE _handle;
        private readonly string _objectPath;

        internal RawNativeHandle(NativeHandles.SYSTEM_HANDLE handle, string objectPath)
        {
            _handle = handle;
            _objectPath = objectPath;
        }

        /// <summary>
        /// The object path that is held by this handle.
        /// </summary>
        public string ObjectPath => _objectPath;

        /// <summary>
        /// The process that holds a handle to this file.
        /// </summary>
        public int ProcessId => unchecked((int)_handle.ProcessId);

        NativeHandles.SYSTEM_HANDLE INativeHandleInternal.Handle => _handle;
    }
}
