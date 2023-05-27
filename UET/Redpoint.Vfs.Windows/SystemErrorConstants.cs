namespace Redpoint.Vfs.Windows
{
    /// <remarks>
    /// https://learn.microsoft.com/en-au/windows/win32/debug/system-error-codes--0-499-
    /// </remarks>
    public static class SystemErrorConstants
    {
        /* ERROR_INVALID_FUNCTION */
        public const int InvalidFunction = unchecked((int)0x00000001);
        /* ERROR_ACCESS_DENIED */
        public const int AccessDenied = unchecked((int)0x00000005);
        /* ERROR_HANDLE_EOF */
        public const int EOF = unchecked((int)0x00000026);
        /* ERROR_NOT_SUPPORTED */
        public const int NotSupported = unchecked((int)0x00000032);
        /* ERROR_IO_PENDING */
        public const int IoPending = unchecked((int)0x000003E5);
        /* ERROR_OPERATION_ABORTED */
        public const int OperationAborted = unchecked((int)0x000003E3);
    }
}
