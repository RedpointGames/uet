namespace Redpoint.Vfs.Windows
{
    public static class HResultConstants
    {
        public const int InvalidFunction = unchecked((int)0x80070001);
        public const int AccessDenied = unchecked((int)0x80070005);
        public const int EOF = unchecked((int)0x80070026);
        public const int NotSupported = unchecked((int)0x80070032);
        public const int IoPending = unchecked((int)0x800703E5);
        public const int OperationAborted = unchecked((int)0x800703E3);
    }
}
