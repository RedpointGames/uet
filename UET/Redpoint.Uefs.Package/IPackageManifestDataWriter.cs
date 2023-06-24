namespace Redpoint.Uefs.Package
{
    public interface IPackageManifestDataWriter
    {
        Task WriteData(IPackageWriter packageWriter, PackageManifest packageManifest);
    }
}
