namespace Redpoint.UET.Workspace.Tests
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.VisualStudio.TestPlatform.Utilities;
    using Redpoint.PathResolution;
    using Redpoint.ProcessExecution;
    using Redpoint.Reservation;
    using Redpoint.UET.Core;
    using Redpoint.UET.Workspace.PhysicalGit;
    using Redpoint.UET.Workspace.Reservation;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
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
            var tempPath = Path.Combine(Path.GetTempPath(), "UETTests-CanCheckoutProject");
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
            services.AddUETWorkspace();
            services.AddReservation();

            var sp = services.BuildServiceProvider();
            var reservationManager = sp.GetRequiredService<IReservationManagerFactory>().CreateReservationManager(tempPath);
            var physicalGit = sp.GetRequiredService<IPhysicalGitCheckout>();

            var reservation = await reservationManager.ReserveAsync("CanCheckoutProject");
            await DirectoryAsync.DeleteAsync(reservation.ReservedPath, true);
            Directory.CreateDirectory(reservation.ReservedPath);

            await physicalGit.PrepareGitWorkspaceAsync(
                reservation,
                new Descriptors.GitWorkspaceDescriptor
                {
                    RepositoryUrl = "https://src.redpoint.games/redpointgames/examples",
                    RepositoryCommitOrRef = "main",
                    WorkspaceDisambiguators = new string[0],
                    AdditionalFolderLayers = new string[0],
                    ProjectFolderName = "GMF",
                    IsEngineBuild = false,
                },
                CancellationToken.None);

            Assert.True(Directory.Exists(Path.Combine(reservation.ReservedPath, ".git")), $"Expected directory {Path.Combine(reservation.ReservedPath, ".git")} to exist.");
            Assert.True(Directory.Exists(Path.Combine(reservation.ReservedPath, "GMF")), $"Expected directory {Path.Combine(reservation.ReservedPath, "GMF")} to exist.");
            Assert.False(Path.Exists(Path.Combine(reservation.ReservedPath, "BuildScripts", ".git")), $"Expected path {Path.Combine(reservation.ReservedPath, "BuildScripts", ".git")} to not exist.");
        }

        [SkippableFact]
        public async Task CanCheckoutWithGitCheckoutMissing()
        {
            var tempPath = Path.Combine(Path.GetTempPath(), "UETTests-CanCheckoutProject");
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
            services.AddUETWorkspace();
            services.AddReservation();

            var sp = services.BuildServiceProvider();
            var reservationManager = sp.GetRequiredService<IReservationManagerFactory>().CreateReservationManager(tempPath);
            var physicalGit = sp.GetRequiredService<IPhysicalGitCheckout>();

            var reservation = await reservationManager.ReserveAsync("CanCheckoutProject");
            await DirectoryAsync.DeleteAsync(reservation.ReservedPath, true);
            Directory.CreateDirectory(reservation.ReservedPath);

            await physicalGit.PrepareGitWorkspaceAsync(
                reservation,
                new Descriptors.GitWorkspaceDescriptor
                {
                    RepositoryUrl = "https://src.redpoint.games/redpointgames/examples",
                    RepositoryCommitOrRef = "main",
                    WorkspaceDisambiguators = new string[0],
                    AdditionalFolderLayers = new string[0],
                    ProjectFolderName = "GMF",
                    IsEngineBuild = false,
                },
                CancellationToken.None);

            // Delete .gitcheckout.
            File.Delete(Path.Combine(reservation.ReservedPath, ".gitcheckout"));

            // Delete some other random content to make sure it gets checked out again.
            File.Delete(Path.Combine(reservation.ReservedPath, "README.md"));

            await physicalGit.PrepareGitWorkspaceAsync(
                reservation,
                new Descriptors.GitWorkspaceDescriptor
                {
                    RepositoryUrl = "https://src.redpoint.games/redpointgames/examples",
                    RepositoryCommitOrRef = "main",
                    WorkspaceDisambiguators = new string[0],
                    AdditionalFolderLayers = new string[0],
                    ProjectFolderName = "GMF",
                    IsEngineBuild = false,
                },
                CancellationToken.None);

            Assert.True(Directory.Exists(Path.Combine(reservation.ReservedPath, ".git")), $"Expected directory {Path.Combine(reservation.ReservedPath, ".git")} to exist.");
            Assert.True(Directory.Exists(Path.Combine(reservation.ReservedPath, "GMF")), $"Expected directory {Path.Combine(reservation.ReservedPath, "GMF")} to exist.");
            Assert.False(Path.Exists(Path.Combine(reservation.ReservedPath, "BuildScripts", ".git")), $"Expected path {Path.Combine(reservation.ReservedPath, "BuildScripts", ".git")} to not exist.");
            Assert.True(File.Exists(Path.Combine(reservation.ReservedPath, "README.MD")), $"Expected file {Path.Combine(reservation.ReservedPath, "README.md")} to exist.");
        }
    }
}
