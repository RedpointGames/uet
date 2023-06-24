namespace Redpoint.Uefs.Daemon.PackageStorage
{
    using Redpoint.Git.Native;
    using Redpoint.Uefs.Daemon.PackageFs;

    public interface IPackageStorage
    {
        IPackageFs PackageFs { get; }

        IGitRepoManager GitRepoManager { get; }

        void StopProcesses();
    }
}
