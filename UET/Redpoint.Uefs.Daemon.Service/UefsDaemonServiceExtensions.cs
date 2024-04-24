namespace Redpoint.Uefs.Daemon.Service
{
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.Uefs.Daemon.Abstractions;
    using Redpoint.Uefs.Daemon.Service.Mounting;
    using Redpoint.Uefs.Daemon.Service.Pulling;
    using Redpoint.Uefs.Protocol;

    public static class UefsDaemonServiceExtensions
    {
        public static void AddUefsService(this IServiceCollection services)
        {
            services.AddSingleton<IWriteScratchPath, DefaultWriteScratchPath>();

            services.AddSingleton<IPuller<PullPackageTagRequest>, PackageTagPuller>();
            services.AddSingleton<IPuller<PullGitCommitRequest>, GitCommitPuller>();

            services.AddSingleton<IMounter<MountPackageFileRequest>, PackageFileMounter>();
            services.AddSingleton<IMounter<MountPackageTagRequest>, PackageTagMounter>();
            services.AddSingleton<IMounter<MountGitCommitRequest>, GitCommitMounter>();
            services.AddSingleton<IMounter<MountGitHubCommitRequest>, GitHubCommitMounter>();
            services.AddSingleton<IMounter<MountFolderSnapshotRequest>, FolderSnapshotMounter>();

            services.AddSingleton<IUefsDaemonFactory, UefsDaemonFactory>();

            services.AddTransient<UefsGrpcService, UefsGrpcService>();

#if GIT_NATIVE_CODE_ENABLED
            services.AddSingleton<IGitVfsSetup, DefaultGitVfsSetup>();
#endif
        }
    }
}
