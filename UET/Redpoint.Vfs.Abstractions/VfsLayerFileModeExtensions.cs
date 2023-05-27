namespace Redpoint.Vfs.Abstractions
{
    public static class VfsLayerFileModeExtensions
    {
        public static bool IsReadOnlyAccess(this FileMode fileMode, FileAccess fileAccess)
        {
            switch (fileMode)
            {
                case FileMode.CreateNew:
                case FileMode.Create:
                case FileMode.Truncate:
                case FileMode.Append:
                    return false;
                case FileMode.Open:
                case FileMode.OpenOrCreate:
                    return fileAccess == FileAccess.Read;
            }
            return false;
        }
    }
}
