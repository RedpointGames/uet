namespace Redpoint.Uefs.Package
{
    public interface IPackageManifestAssembler
    {
        PackageManifest CreateManifestFromSourceDirectory(IPackageWriter packageWriter, string path);
    }
}
