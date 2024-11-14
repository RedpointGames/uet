namespace Redpoint.Uet.Workspace.Tests
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.CredentialDiscovery;
    using Redpoint.IO;
    using Redpoint.PathResolution;
    using Redpoint.ProcessExecution;
    using Redpoint.Reservation;
    using Redpoint.Uefs.Protocol;
    using Redpoint.Uet.Core;
    using Redpoint.Uet.Workspace.PhysicalGit;
    using System;
    using System.Threading.Tasks;
    using Xunit.Abstractions;

    public class PhysicalGitCheckoutTests
    {
        private readonly ITestOutputHelper _output;

        public PhysicalGitCheckoutTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [SkippableFact]
        public async Task CanCheckoutFresh()
        {
            Skip.IfNot(OperatingSystem.IsWindows());
            Skip.IfNot(Directory.Exists(@"C:\Work\internal"));
            var tempPath = Path.Combine(Path.GetTempPath(), "UETTests-" + nameof(PhysicalGitCheckoutTests));
            Directory.CreateDirectory(tempPath);

            var services = new ServiceCollection();
            services.AddLogging(builder =>
            {
                builder.ClearProviders();
                builder.SetMinimumLevel(LogLevel.Trace);
                builder.AddXUnit(
                    _output,
                    configure =>
                    {
                    });
            });
            services.AddPathResolution();
            services.AddProcessExecution();
            services.AddUefs();
            services.AddUETWorkspace();
            services.AddUETCore(skipLoggingRegistration: true);
            services.AddReservation();
            services.AddCredentialDiscovery();

            var sp = services.BuildServiceProvider();
            var reservationManager = sp.GetRequiredService<IReservationManagerFactory>().CreateReservationManager(tempPath);
            var physicalGit = sp.GetRequiredService<IPhysicalGitCheckout>();

            var reservation = await reservationManager.ReserveAsync(nameof(CanCheckoutFresh));
            await DirectoryAsync.DeleteAsync(reservation.ReservedPath, true);
            Directory.CreateDirectory(reservation.ReservedPath);

            await physicalGit.PrepareGitWorkspaceAsync(
                reservation.ReservedPath,
                new Descriptors.GitWorkspaceDescriptor
                {
                    RepositoryUrl = "https://src.redpoint.games/redpointgames/examples",
                    RepositoryCommitOrRef = "main",
                    WorkspaceDisambiguators = Array.Empty<string>(),
                    AdditionalFolderLayers = Array.Empty<string>(),
                    AdditionalFolderZips = Array.Empty<string>(),
                    ProjectFolderName = "GMF",
                    BuildType = Descriptors.GitWorkspaceDescriptorBuildType.Generic,
                    WindowsSharedGitCachePath = null,
                    MacSharedGitCachePath = null,
                },
                CancellationToken.None);

            Assert.True(Directory.Exists(Path.Combine(reservation.ReservedPath, ".git")), $"Expected directory {Path.Combine(reservation.ReservedPath, ".git")} to exist.");
            Assert.True(Directory.Exists(Path.Combine(reservation.ReservedPath, "GMF")), $"Expected directory {Path.Combine(reservation.ReservedPath, "GMF")} to exist.");
            Assert.False(Path.Exists(Path.Combine(reservation.ReservedPath, "BuildScripts", ".git")), $"Expected path {Path.Combine(reservation.ReservedPath, "BuildScripts", ".git")} to not exist.");
        }

        [SkippableFact]
        public async Task CanCheckoutWithGitCheckoutMissing()
        {
            Skip.IfNot(OperatingSystem.IsWindows());
            Skip.IfNot(Directory.Exists(@"C:\Work\internal"));
            var tempPath = Path.Combine(Path.GetTempPath(), "UETTests-" + nameof(PhysicalGitCheckoutTests));
            Directory.CreateDirectory(tempPath);

            var services = new ServiceCollection();
            services.AddLogging(builder =>
            {
                builder.ClearProviders();
                builder.SetMinimumLevel(LogLevel.Trace);
                builder.AddXUnit(
                    _output,
                    configure =>
                    {
                    });
            });
            services.AddPathResolution();
            services.AddProcessExecution();
            services.AddUefs();
            services.AddUETWorkspace();
            services.AddUETCore(skipLoggingRegistration: true);
            services.AddReservation();
            services.AddCredentialDiscovery();

            var sp = services.BuildServiceProvider();
            var reservationManager = sp.GetRequiredService<IReservationManagerFactory>().CreateReservationManager(tempPath);
            var physicalGit = sp.GetRequiredService<IPhysicalGitCheckout>();

            var reservation = await reservationManager.ReserveAsync(nameof(CanCheckoutWithGitCheckoutMissing));
            await DirectoryAsync.DeleteAsync(reservation.ReservedPath, true);
            Directory.CreateDirectory(reservation.ReservedPath);

            await physicalGit.PrepareGitWorkspaceAsync(
                reservation.ReservedPath,
                new Descriptors.GitWorkspaceDescriptor
                {
                    RepositoryUrl = "https://src.redpoint.games/redpointgames/examples",
                    RepositoryCommitOrRef = "main",
                    WorkspaceDisambiguators = Array.Empty<string>(),
                    AdditionalFolderLayers = Array.Empty<string>(),
                    AdditionalFolderZips = Array.Empty<string>(),
                    ProjectFolderName = "GMF",
                    BuildType = Descriptors.GitWorkspaceDescriptorBuildType.Generic,
                    WindowsSharedGitCachePath = null,
                    MacSharedGitCachePath = null,
                },
                CancellationToken.None);

            // Delete .gitcheckout.
            File.Delete(Path.Combine(reservation.ReservedPath, ".gitcheckout"));

            // Delete some other random content to make sure it gets checked out again.
            File.Delete(Path.Combine(reservation.ReservedPath, "README.md"));

            await physicalGit.PrepareGitWorkspaceAsync(
                reservation.ReservedPath,
                new Descriptors.GitWorkspaceDescriptor
                {
                    RepositoryUrl = "https://src.redpoint.games/redpointgames/examples",
                    RepositoryCommitOrRef = "main",
                    WorkspaceDisambiguators = Array.Empty<string>(),
                    AdditionalFolderLayers = Array.Empty<string>(),
                    AdditionalFolderZips = Array.Empty<string>(),
                    ProjectFolderName = "GMF",
                    BuildType = Descriptors.GitWorkspaceDescriptorBuildType.Generic,
                    WindowsSharedGitCachePath = null,
                    MacSharedGitCachePath = null,
                },
                CancellationToken.None);

            Assert.True(Directory.Exists(Path.Combine(reservation.ReservedPath, ".git")), $"Expected directory {Path.Combine(reservation.ReservedPath, ".git")} to exist.");
            Assert.True(Directory.Exists(Path.Combine(reservation.ReservedPath, "GMF")), $"Expected directory {Path.Combine(reservation.ReservedPath, "GMF")} to exist.");
            Assert.False(Path.Exists(Path.Combine(reservation.ReservedPath, "BuildScripts", ".git")), $"Expected path {Path.Combine(reservation.ReservedPath, "BuildScripts", ".git")} to not exist.");
            Assert.True(File.Exists(Path.Combine(reservation.ReservedPath, "README.MD")), $"Expected file {Path.Combine(reservation.ReservedPath, "README.md")} to exist.");
        }

        [SkippableFact]
        public async Task CanCheckoutOverSsh()
        {
            // @note: This should be a read-only GitHub deploy key to the UET repository.
            var sshTestPrivateKeyPath = Environment.GetEnvironmentVariable("SSH_TEST_REDPOINT_CREDENTIAL_DISCOVERY_SSH_PRIVATE_KEY_PATH_github_com");
            var sshTestPublicKeyPath = Environment.GetEnvironmentVariable("SSH_TEST_REDPOINT_CREDENTIAL_DISCOVERY_SSH_PUBLIC_KEY_PATH_github_com");
            Skip.If(
                string.IsNullOrWhiteSpace(sshTestPrivateKeyPath) || string.IsNullOrWhiteSpace(sshTestPublicKeyPath),
                "Need to have the private and public keys set via environment variables to run this test.");

            var tempPath = Path.Combine(Path.GetTempPath(), "UETTests-" + nameof(PhysicalGitCheckoutTests));
            Directory.CreateDirectory(tempPath);

            var services = new ServiceCollection();
            services.AddLogging(builder =>
            {
                builder.ClearProviders();
                builder.SetMinimumLevel(LogLevel.Trace);
                builder.AddXUnit(
                    _output,
                    configure =>
                    {
                    });
            });
            services.AddPathResolution();
            services.AddProcessExecution();
            services.AddUefs();
            services.AddUETWorkspace();
            services.AddUETCore(skipLoggingRegistration: true);
            services.AddReservation();
            services.AddCredentialDiscovery();

            var sp = services.BuildServiceProvider();
            var reservationManager = sp.GetRequiredService<IReservationManagerFactory>().CreateReservationManager(tempPath);
            var physicalGit = sp.GetRequiredService<IPhysicalGitCheckout>();

            var reservation = await reservationManager.ReserveAsync(nameof(CanCheckoutOverSsh));
            await DirectoryAsync.DeleteAsync(reservation.ReservedPath, true);
            Directory.CreateDirectory(reservation.ReservedPath);

            Environment.SetEnvironmentVariable(
                "REDPOINT_CREDENTIAL_DISCOVERY_SSH_PRIVATE_KEY_PATH_github_com",
                sshTestPrivateKeyPath);
            Environment.SetEnvironmentVariable(
                "REDPOINT_CREDENTIAL_DISCOVERY_SSH_PUBLIC_KEY_PATH_github_com",
                sshTestPublicKeyPath);

            await physicalGit.PrepareGitWorkspaceAsync(
                reservation.ReservedPath,
                new Descriptors.GitWorkspaceDescriptor
                {
                    RepositoryUrl = "ssh://git@github.com/RedpointGames/uet",
                    RepositoryCommitOrRef = "main",
                    WorkspaceDisambiguators = Array.Empty<string>(),
                    AdditionalFolderLayers = Array.Empty<string>(),
                    AdditionalFolderZips = Array.Empty<string>(),
                    ProjectFolderName = "UET",
                    BuildType = Descriptors.GitWorkspaceDescriptorBuildType.Generic,
                    WindowsSharedGitCachePath = null,
                    MacSharedGitCachePath = null,
                },
                CancellationToken.None);

            Assert.True(Directory.Exists(Path.Combine(reservation.ReservedPath, ".git")), $"Expected directory {Path.Combine(reservation.ReservedPath, ".git")} to exist.");
        }

        [SkippableFact(Skip = "Test seems to be flaky on the build servers.")]
        public async Task CanCheckoutEngineFresh()
        {
            var tempPath = Path.Combine(Path.GetTempPath(), "UETTests-" + nameof(PhysicalGitCheckoutTests));
            Directory.CreateDirectory(tempPath);

            var services = new ServiceCollection();
            services.AddLogging(builder =>
            {
                builder.ClearProviders();
                builder.SetMinimumLevel(LogLevel.Trace);
                builder.AddXUnit(
                    _output,
                    configure =>
                    {
                    });
            });
            services.AddPathResolution();
            services.AddProcessExecution();
            services.AddUefs();
            services.AddUETWorkspace();
            services.AddUETCore(skipLoggingRegistration: true);
            services.AddReservation();
            services.AddCredentialDiscovery();

            var sp = services.BuildServiceProvider();
            var reservationManager = sp.GetRequiredService<IReservationManagerFactory>().CreateReservationManager(tempPath);
            var physicalGit = sp.GetRequiredService<IPhysicalGitCheckout>();

            await using var reservation = await reservationManager.ReserveAsync(nameof(CanCheckoutEngineFresh));
            await using var sharedReservation = await reservationManager.ReserveAsync(nameof(CanCheckoutEngineFresh) + "Shared");
            await DirectoryAsync.DeleteAsync(reservation.ReservedPath, true);
            Directory.CreateDirectory(reservation.ReservedPath);

            await physicalGit.PrepareGitWorkspaceAsync(
                reservation.ReservedPath,
                new Descriptors.GitWorkspaceDescriptor
                {
                    RepositoryUrl = "https://src.redpoint.games/redpointgames/uet",
                    RepositoryCommitOrRef = "main",
                    WorkspaceDisambiguators = Array.Empty<string>(),
                    AdditionalFolderLayers = Array.Empty<string>(),
                    AdditionalFolderZips = Array.Empty<string>(),
                    ProjectFolderName = null,
                    BuildType = Descriptors.GitWorkspaceDescriptorBuildType.Engine,
                    WindowsSharedGitCachePath = sharedReservation.ReservedPath,
                    MacSharedGitCachePath = sharedReservation.ReservedPath,
                },
                CancellationToken.None);

            Assert.True(Directory.Exists(Path.Combine(sharedReservation.ReservedPath, "Git", "objects")), $"Expected directory {Path.Combine(sharedReservation.ReservedPath, "Git", "objects")} to exist.");
            Assert.True(File.Exists(Path.Combine(reservation.ReservedPath, "README.md")), $"Expected file {Path.Combine(reservation.ReservedPath, "README.md")} to exist.");
        }
    }
}
