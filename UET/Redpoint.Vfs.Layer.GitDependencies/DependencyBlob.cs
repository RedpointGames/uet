namespace Redpoint.Vfs.Layer.GitDependencies
{
    internal sealed class DependencyBlob
    {
        public string? Hash;
        public long Size;
        public string? PackHash;
        public long PackOffset;
    }
}
