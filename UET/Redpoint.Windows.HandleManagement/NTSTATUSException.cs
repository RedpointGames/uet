#pragma warning disable CA1707 // Identifiers should not contain underscores

namespace Redpoint.Windows.HandleManagement
{
    using global::Windows.Win32.Foundation;

    /// <summary>
    /// Thrown when low-level APIs (such as NtDuplicateObject) fail.
    /// </summary>
    /// <remarks>
    /// The constants defined in this class are obtained from:
    /// https://gist.github.com/mbrownnycnyc/24cb49aa2cba35be4764b1fa37bf9063
    /// </remarks>
    public class NTSTATUSException : Exception
    {
        /// <summary>
        /// The handle is invalid.
        /// </summary>
        public const uint NT_STATUS_INVALID_HANDLE = 0xC0000008u;

        /// <summary>
        /// Access is denied.
        /// </summary>
        public const uint NT_STATUS_ACCESS_DENIED = 0xC0000022u;

        /// <summary>
        /// The network request is not supported.
        /// </summary>
        public const uint NT_STATUS_NOT_SUPPORTED = 0xC00000BBu;

        /// <summary>
        /// A device attached to the system is not functioning.
        /// </summary>
        public const uint NT_STATUS_UNSUCCESSFUL = 0xC0000001u;

        /// <summary>
        /// The specified path is invalid.
        /// </summary>
        public const uint NT_STATUS_OBJECT_PATH_INVALID = 0xC0000039u;

        /// <summary>
        /// The I/O operation has been aborted because of either a thread exit or an application request.
        /// </summary>
        public const uint NT_STATUS_CANCELLED = 0xC0000120u;

        /// <summary>
        /// No process is on the other end of the pipe.
        /// </summary>
        public const uint NT_STATUS_PIPE_DISCONNECTED = 0xC00000B0u;

        /// <summary>
        /// Access is denied.
        /// </summary>
        public const uint NT_STATUS_PROCESS_IS_TERMINATING = 0xC000010Au;

        /// <summary>
        /// An operation was attempted to a volume after it was dismounted.
        /// </summary>
        public const uint NT_STATUS_VOLUME_DISMOUNTED = 0xC000026Eu;

        /// <summary>
        /// An illegal operation was attempted on a registry key that has been marked for deletion.
        /// </summary>
        public const uint NT_STATUS_KEY_DELETED = 0xC000017Cu;

        /// <summary>
        /// The raw NTSTATUS code.
        /// </summary>
        public uint StatusCode { get; private set; }

        internal NTSTATUSException(NTSTATUS status) : base($"NTSTATUS failure: 0x{(uint)status.Value:X}")
        {
            unchecked
            {
                StatusCode = (uint)status.Value;
            }
        }
    }
}
