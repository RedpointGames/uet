namespace Redpoint.Vfs.Abstractions
{
    /// <summary>
    /// Provides helper functions for virtual filesystem layer implementations.
    /// </summary>
    public static class VfsLayerFileModeExtensions
    {
        /// <summary>
        /// Returns whether the provided file mode and file access effectively results in a read-only operation.
        /// </summary>
        /// <param name="fileMode">The file mode.</param>
        /// <param name="fileAccess">The file access.</param>
        /// <returns>Whether the operation is a read-only operation.</returns>
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
