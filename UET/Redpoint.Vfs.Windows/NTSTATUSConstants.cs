namespace Redpoint.Vfs.Windows
{
    /// <remarks>
    /// From WinFsp FileSystemBase+Const.cs
    /// </remarks>
    public static class NTSTATUSConstants
    {
        /* STATUS_PENDING */
        public const int Pending = unchecked((int)0x00000103);
        /* STATUS_END_OF_FILE */
        public const int EOF = unchecked((int)0xc0000011);
        /* STATUS_ACCESS_DENIED */
        public const int AccessDenied = unchecked((int)0xc0000022);
        /* STATUS_CANCELLED */
        public const int Cancelled = unchecked((int)0xc0000120);
    }
}
