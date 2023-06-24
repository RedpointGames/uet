namespace Redpoint.Uefs.Daemon.PackageFs
{
    using System.Runtime.Versioning;

    public interface IPackageFsFactory
    {
        IPackageFs CreateLocallyBackedPackageFs(string storagePath);

        [SupportedOSPlatform("windows6.2")]
        IPackageFs CreateVfsBackedPackageFs(string storagePath);
    }
}
