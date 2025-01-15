namespace Redpoint.CloudFramework.CLI
{
    using System.Threading.Tasks;

    internal interface IYarnInstallationService
    {
        Task<(int exitCode, string? yarnPath)> InstallYarnIfNeededAsync(CancellationToken cancellationToken);
    }
}
