namespace Redpoint.Uefs.Package
{
    public delegate void OnFileBytesWritten(long bytes);
    public delegate void OnFileWriteComplete(string pathInPackage);

    public interface IPackageWriter : IDisposable
    {
        long IndexHeaderSize { get; }

        long ComputeEntryIndexSize(string pathInPackage);

        long ComputeEntryDataSize(string pathInPackage, long size);

        void OpenPackageForWriting(string packagePath, long indexSize, long dataSize);

        ValueTask WritePackageIndex(PackageManifest packageManifest);

        bool WantsDirectoryWrites { get; }

        bool SupportsParallelDirectoryWrites { get; }

        bool SupportsParallelWrites { get; }

        ValueTask WritePackageDirectory(PackageManifestEntry packageManifestEntry, OnFileWriteComplete onDirectoryWriteComplete);

        ValueTask WritePackageFile(PackageManifestEntry packageManifestEntry, OnFileBytesWritten onFileBytesWritten, OnFileWriteComplete onFileWriteComplete);
    }
}
