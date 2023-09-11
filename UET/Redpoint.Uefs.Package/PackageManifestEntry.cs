namespace Redpoint.Uefs.Package
{
    public class PackageManifestEntry
    {
        public PackageManifestEntry(string sourcePath, string pathInPackage, long offset, long size)
        {
            SourcePath = sourcePath;
            PathInPackage = pathInPackage;
            OffsetBytes = offset;
            LengthBytes = size;
        }

        public string PathInPackage { get; }
        public string SourcePath { get; }
        public long OffsetBytes { get; }
        public long LengthBytes { get; }
        public bool IsDirectory => string.IsNullOrEmpty(SourcePath);
    }
}
