
namespace Redpoint.Vfs.Windows
{
    using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public ref struct NativeFileEndOfFileInfo
    {
        public long EndOfFile;
    }
}
