namespace Redpoint.Git.Packfile
{
    using System.Runtime.InteropServices;

    /// <summary>
    /// Represents a SHA1 entry from a Git packfile index.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct PackfileIndexShaEntry
    {
        /// <summary>
        /// Bytes representing the SHA1.
        /// </summary>
        public fixed byte Sha[20];
    }
}