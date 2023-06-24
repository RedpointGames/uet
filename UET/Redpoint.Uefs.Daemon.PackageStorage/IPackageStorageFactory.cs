namespace Redpoint.Uefs.Daemon.PackageStorage
{
    public interface IPackageStorageFactory
    {
        IPackageStorage CreatePackageStorage(string storagePath);
    }
}
