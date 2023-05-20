namespace Redpoint.Windows.HandleManagement
{
    using System.Runtime.Versioning;

    /// <summary>
    /// Represents a handle that points specifically to an open file.
    /// </summary>
    [SupportedOSPlatform("windows6.2")]
    public class FileNativeHandle : INativeHandleInternal
    {
        internal readonly NativeHandles.SYSTEM_HANDLE _handle;
        private readonly string _filePath;

        internal FileNativeHandle(NativeHandles.SYSTEM_HANDLE handle, string filePath)
        {
            _handle = handle;
            _filePath = filePath;
        }

        /// <summary>
        /// The file path that is held by this handle.
        /// </summary>
        public string FilePath => _filePath;

        /// <summary>
        /// The process that holds a handle to this file.
        /// </summary>
        public int ProcessId => unchecked((int)_handle.ProcessId);

        NativeHandles.SYSTEM_HANDLE INativeHandleInternal.Handle => _handle;
    }
}
