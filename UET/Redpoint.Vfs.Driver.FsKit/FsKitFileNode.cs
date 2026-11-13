#pragma warning disable APL0002

namespace Redpoint.Vfs.Driver.FsKit
{
    using Darwin;
    using FSKit;
    using Redpoint.Vfs.Abstractions;

    public sealed class FsKitFileNode : FSItem
    {
        private static FSItemId GetParentId(string path)
        {
            var parentPath = System.IO.Path.GetDirectoryName(path);

            if (parentPath == null)
            {
                if (path == "/")
                {
                    return FSItemId.ParentOfRoot;
                }
                else
                {
                    return FSItemId.RootDirectory;
                }
            }
            else
            {
                ulong hash = (ulong)(path.Replace('\\', '/').GetHashCode(StringComparison.OrdinalIgnoreCase));
                hash %= ulong.MaxValue - 3;
                return (FSItemId)(hash + 3);
            }
        }

        internal static FSItemId GetFileId(string path)
        {
            if (path == "/")
            {
                return FSItemId.RootDirectory;
            }
            else
            {
                ulong hash = (ulong)(path.Replace('\\', '/').GetHashCode(StringComparison.OrdinalIgnoreCase));
                hash %= ulong.MaxValue - 3;
                return (FSItemId)(hash + 3);
            }
        }

        private static uint _rwxEveryone = (uint)(
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
            UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute |
            UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute);

        private static TimeSpec ToTimeSpec(DateTimeOffset dateTimeOffset)
        {
            var totalMilliseconds = dateTimeOffset.ToUnixTimeMilliseconds();
            return new TimeSpec
            {
                Seconds = (nint)(totalMilliseconds / 1000),
                NanoSeconds = (nint)(totalMilliseconds % 1000) * 1_000_000,
            };
        }

        internal static FSItemAttributes GetItemAttributes(string path, VfsEntry metadata)
        {
            if (metadata.IsDirectory)
            {
                return new FSItemAttributes
                {
                    ParentId = GetParentId(path),
                    FileId = GetFileId(path),
                    Uid = 0,
                    Gid = 0,
                    LinkCount = 1,
                    Type = FSItemType.Directory,
                    Mode = (uint)(FileTypes.S_IFDIR | _rwxEveryone),
                    AllocSize = 0,
                    Size = 0,
                    AddedTime = ToTimeSpec(metadata.CreationTime),
                    ModifyTime = ToTimeSpec(metadata.LastWriteTime),
                    AccessTime = ToTimeSpec(metadata.LastAccessTime),
                    BirthTime = ToTimeSpec(metadata.CreationTime),
                };
            }
            else
            {
                return new FSItemAttributes
                {
                    ParentId = GetParentId(path),
                    FileId = GetFileId(path),
                    Uid = 0,
                    Gid = 0,
                    LinkCount = 1,
                    Type = FSItemType.File,
                    Mode = (uint)(FileTypes.S_IFREG | _rwxEveryone),
                    AllocSize = FsKitVolume.GetAllocationSize((ulong)metadata.Size),
                    Size = (ulong)metadata.Size,
                    AddedTime = ToTimeSpec(metadata.CreationTime),
                    ModifyTime = ToTimeSpec(metadata.LastWriteTime),
                    AccessTime = ToTimeSpec(metadata.LastAccessTime),
                    BirthTime = ToTimeSpec(metadata.CreationTime),
                };
            }
        }

        internal static FsKitFileNode AsNewDirectoryWithPath(
            string path,
            DateTimeOffset createdTime)
        {
            return new FsKitFileNode(
                path,
                new FSItemAttributes
                {
                    ParentId = GetParentId(path),
                    FileId = GetFileId(path),
                    Uid = 0,
                    Gid = 0,
                    LinkCount = 1,
                    Type = FSItemType.Directory,
                    Mode = (uint)(FileTypes.S_IFDIR | _rwxEveryone),
                    AllocSize = 0,
                    Size = 0,
                    AddedTime = ToTimeSpec(createdTime),
                    ModifyTime = ToTimeSpec(createdTime),
                    AccessTime = ToTimeSpec(createdTime),
                    BirthTime = ToTimeSpec(createdTime),
                },
                null);
        }

        internal static FsKitFileNode AsExistingDirectoryWithPath(
            string path,
            VfsEntry metadata)
        {
            return new FsKitFileNode(
                path,
                GetItemAttributes(path, metadata),
                null);
        }

        internal static FsKitFileNode AsFileWithPath(
            string path,
            VfsEntry metadata)
        {
            return new FsKitFileNode(
                path,
                GetItemAttributes(path, metadata),
                null);
        }

        private FsKitFileNode(
            string path,
            FSItemAttributes fileAttributes,
            IVfsFileHandle<IVfsFile>? projectedFileHandle)
        {
            Name = new FSFileName(System.IO.Path.GetFileName(path));
            Path = path;
            Attributes = fileAttributes;
            ProjectedFileHandle = projectedFileHandle;
        }

        protected override void Dispose(bool disposing)
        {
            ProjectedFileHandle?.Dispose();
            ProjectedFileHandle = null;

            base.Dispose(disposing);
        }

        public FSFileName Name { get; }

        public string Path { get; }

        public bool IsDirectory => Attributes.Type == FSItemType.Directory;

        public FSItemAttributes Attributes;

        public IVfsFileHandle<IVfsFile>? ProjectedFileHandle { get; set; }

        public int OpenCount { get; set; }
    }
}
