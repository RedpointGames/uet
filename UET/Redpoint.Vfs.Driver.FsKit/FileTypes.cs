#pragma warning disable APL0002

namespace Redpoint.Vfs.Driver.FsKit
{
    internal static class FileTypes
    {
        internal const int S_IFMT = 0xF000;
        internal const int S_IFIFO = 0x1000;
        internal const int S_IFCHR = 0x2000;
        internal const int S_IFDIR = 0x4000;
        internal const int S_IFBLK = 0x6000;
        internal const int S_IFREG = 0x8000;
        internal const int S_IFLNK = 0xA000;
        internal const int S_IFSOCK = 0xC000;
    }
}
