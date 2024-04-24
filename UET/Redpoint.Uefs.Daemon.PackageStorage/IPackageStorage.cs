namespace Redpoint.Uefs.Daemon.PackageStorage
{
    using Redpoint.Uefs.Daemon.PackageFs;

    public interface IPackageStorage
    {
        IPackageFs PackageFs { get; }

        void StopProcesses();
    }
}
