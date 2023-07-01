namespace Redpoint.Uefs.Package
{
    using System.Collections;

    public class PackageManifest : IEnumerable<PackageManifestEntry>
    {
        private Dictionary<string, PackageManifestEntry> _manifestData = new Dictionary<string, PackageManifestEntry>();

        public long IndexSizeBytes { get; set; }
        public long DataSizeBytes { get; set; }
        public long FileCount { get; set; }

        public void AddFileToManifest(string pathInPackage, string sourcePath, long offsetBytes, long lengthBytes)
        {
            _manifestData.Add(pathInPackage, new PackageManifestEntry(sourcePath, pathInPackage, offsetBytes, lengthBytes));
        }

        public void AddDirectoryToManifest(string pathInPackage)
        {
            _manifestData.Add(pathInPackage, new PackageManifestEntry(string.Empty, pathInPackage, 0, 0));
        }

        public IEnumerator<PackageManifestEntry> GetEnumerator()
        {
            return _manifestData.Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _manifestData.Values.GetEnumerator();
        }

        public long Count => _manifestData.Count;
    }
}
