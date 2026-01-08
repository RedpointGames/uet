namespace UET.Commands.Internal
{
    using Redpoint.CommandLine;
    using Redpoint.KubernetesManager.HostedService.Rkm;
    using Redpoint.KubernetesManager.PxeBoot;
    using System.CommandLine;
    using UET.Commands.Internal.CIBuild;
    using UET.Commands.Internal.CMakeUbaRun;
    using UET.Commands.Internal.CMakeUbaServer;
    using UET.Commands.Internal.CopyAndMutateBuildCs;
    using UET.Commands.Internal.CreateGitHubRelease;
    using UET.Commands.Internal.CreateJunction;
    using UET.Commands.Internal.DynamicReentrantTask;
    using UET.Commands.Internal.EngineCheckout;
    using UET.Commands.Internal.EnginePerforceToGit;
    using UET.Commands.Internal.GenerateJsonSchema;
    using UET.Commands.Internal.GitCredentialHelper;
    using UET.Commands.Internal.InspectSnmp;
    using UET.Commands.Internal.InstallPackage;
    using UET.Commands.Internal.InstallPlatformSdk;
    using UET.Commands.Internal.ListCustomObjects;
    using UET.Commands.Internal.Patch;
    using UET.Commands.Internal.PxeBoot;
    using UET.Commands.Internal.RegisterGitLabRunner;
    using UET.Commands.Internal.RemoteZfsServer;
    using UET.Commands.Internal.RemoteZfsTest;
    using UET.Commands.Internal.RemoveStalePrecompiledHeaders;
    using UET.Commands.Internal.ReparentAdditionalPropertiesInTargets;
    using UET.Commands.Internal.RunDownstreamTest;
    using UET.Commands.Internal.RunDriveMappedProcess;
    using UET.Commands.Internal.RunGauntletTestFromBuildGraph;
    using UET.Commands.Internal.RunRemote;
    using UET.Commands.Internal.RunRemoteHost;
    using UET.Commands.Internal.Service;
    using UET.Commands.Internal.SetFilterFile;
    using UET.Commands.Internal.TestAutoDiscovery;
    using UET.Commands.Internal.TestDatabaseLibrary;
    using UET.Commands.Internal.TestGrpcPipes;
    using UET.Commands.Internal.TestPdu;
    using UET.Commands.Internal.TestUba;
    using UET.Commands.Internal.TestUefsConnection;
    using UET.Commands.Internal.Tpm;
    using UET.Commands.Internal.UpdateCopyrightHeadersForSubmission;
    using UET.Commands.Internal.UpdateUPlugin;
    using UET.Commands.Internal.UploadToBackblazeB2;
    using UET.Commands.Internal.VerifyDllFileIntegrity;
    using UET.Commands.Internal.WakeOnLan;
    using UET.Commands.Internal.WindowsImaging;

    internal sealed class InternalCommand : ICommandDescriptorProvider<UetGlobalCommandContext>
    {
        public static CommandDescriptor<UetGlobalCommandContext> Descriptor => UetCommandDescriptor.NewBuilder()
            .WithCommand(
                builder =>
                {
                    builder.AddCommand<CIBuildCommand>();
                    builder.AddCommand<CopyAndMutateBuildCsCommand>();
                    builder.AddCommand<RunDownstreamTestCommand>();
                    builder.AddCommand<RunGauntletTestFromBuildGraphCommand>();
                    builder.AddCommand<SetFilterFileCommand>();
                    builder.AddCommand<UpdateCopyrightHeadersForSubmissionCommand>();
                    builder.AddCommand<UpdateUPluginCommand>();
                    builder.AddCommand<UploadToBackblazeB2Command>();
                    builder.AddCommand<TestAutoDiscoveryCommand>();
                    builder.AddCommand<TestGrpcPipesCommand>();
                    builder.AddCommand<TestUefsConnectionCommand>();
                    builder.AddCommand<CreateGitHubReleaseCommand>();
                    builder.AddCommand<RunDynamicReentrantTaskCommand>();
                    builder.AddCommand<GenerateJsonSchemaCommand>();
                    builder.AddCommand<RemoveStalePrecompiledHeadersCommand>();
                    builder.AddCommand<InstallPlatformSdkCommand>();
                    builder.AddCommand<RunDriveMappedProcessCommand>();
                    builder.AddCommand<CreateJunctionCommand>();
                    builder.AddCommand<StressTestProcessCommand>();
                    builder.AddCommand<TestPduCommand>();
                    builder.AddCommand<RunRemoteHostCommand>();
                    builder.AddCommand<RunRemoteCommand>();
                    builder.AddCommand<InspectSnmpCommand>();
                    builder.AddCommand<ReparentAdditionalPropertiesInTargetsCommand>();
                    builder.AddCommand<WakeOnLanCommand>();
                    builder.AddCommand<TestUbaCommand>();
                    builder.AddCommand<RemoteZfsServerCommand>();
                    builder.AddCommand<RemoteZfsTestCommand>();
                    builder.AddCommand<EngineCheckoutCommand>();
                    builder.AddCommand<InstallPackageCommand>();
                    builder.AddCommand<CMakeUbaServerCommand>();
                    builder.AddCommand<CMakeUbaRunCommand>();
                    builder.AddCommand<EnginePerforceToGitCommand>();
                    builder.AddCommand<InstallXcodeCommand>();
                    builder.AddCommandWithoutGlobalContext<RkmServiceCommand>();
                    builder.AddCommand<PatchCommand>();
                    builder.AddCommand<ServiceCommand>();
                    builder.AddCommand<GitCredentialHelperCommand>();
                    builder.AddCommand<RegisterGitLabRunnerCommand>();
                    builder.AddCommand<TestDatabaseLibraryCommand>();
                    builder.AddCommand<VerifyDllFileIntegrityCommand>();
                    builder.AddCommand<WindowsImagingCommand>();
                    builder.AddCommand<TpmCommand>();
                    builder.AddCommandWithoutGlobalContext<PxeBootCommand>();
                    builder.AddCommand<ListCustomObjectsCommand>();

                    var command = new Command("internal", "Internal commands used by UET when it needs to call back into itself.");
                    command.IsHidden = true;
                    return command;
                })
            .Build();
    }
}
