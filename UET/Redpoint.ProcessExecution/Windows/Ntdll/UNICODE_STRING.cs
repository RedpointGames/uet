namespace Redpoint.ProcessExecution.Windows.Ntdll
{
    using System;
    using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Sequential)]
    internal readonly unsafe struct UNICODE_STRING
    {
        public readonly ushort Length;
        public readonly ushort MaximumLength;
        public readonly char* Buffer;

        public UNICODE_STRING(char* str, int length)
        {
            Length = (ushort)(length * 2);
            MaximumLength = (ushort)((length * 2) + 1);
            Buffer = str;
        }
    }
}
