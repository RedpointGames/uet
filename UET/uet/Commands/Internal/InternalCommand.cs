namespace UET.Commands.Internal
{
    using System.CommandLine;
    using UET.Commands.Internal.CIBuild;
    using UET.Commands.Internal.CopyAndMutateBuildCs;
    using UET.Commands.Internal.CreateGitHubRelease;
    using UET.Commands.Internal.CreateJunction;
    using UET.Commands.Internal.DynamicReentrantTask;
    using UET.Commands.Internal.GenerateJsonSchema;
    using UET.Commands.Internal.InspectSnmp;
    using UET.Commands.Internal.InstallPlatformSdk;
    using UET.Commands.Internal.RemoteZfsServer;
    using UET.Commands.Internal.RemoteZfsTest;
    using UET.Commands.Internal.RemoveStalePrecompiledHeaders;
    using UET.Commands.Internal.ReparentAdditionalPropertiesInTargets;
    using UET.Commands.Internal.RunDownstreamTest;
    using UET.Commands.Internal.RunDriveMappedProcess;
    using UET.Commands.Internal.RunGauntletTestFromBuildGraph;
    using UET.Commands.Internal.RunRemote;
    using UET.Commands.Internal.RunRemoteHost;
    using UET.Commands.Internal.SetFilterFile;
    using UET.Commands.Internal.TestAutoDiscovery;
    using UET.Commands.Internal.TestGrpcPipes;
    using UET.Commands.Internal.TestPdu;
    using UET.Commands.Internal.TestUefsConnection;
    using UET.Commands.Internal.UpdateCopyrightHeadersForSubmission;
    using UET.Commands.Internal.UpdateUPlugin;
    using UET.Commands.Internal.UploadToBackblazeB2;
    using UET.Commands.Internal.WakeOnLan;
    using UET.Commands.Internal.TestUba;
    using UET.Commands.Internal.EngineCheckout;

    internal sealed class InternalCommand
    {
        public static Command CreateInternalCommand(HashSet<Command> globalCommands)
        {
            var subcommands = new List<Command>
            {
                CIBuildCommand.CreateCIBuildCommand(),
                CopyAndMutateBuildCsCommand.CreateCopyAndMutateBuildCsCommand(),
                RunDownstreamTestCommand.CreateRunDownstreamTestCommand(),
                RunGauntletTestFromBuildGraphCommand.CreateRunGauntletCommand(),
                SetFilterFileCommand.CreateSetFilterFileCommand(),
                UpdateCopyrightHeadersForSubmissionCommand.CreateUpdateCopyrightHeadersForSubmissionCommand(),
                UpdateUPluginCommand.CreateUpdateUPluginCommand(),
                UploadToBackblazeB2Command.CreateUploadToBackblazeB2Command(),
                TestAutoDiscoveryCommand.CreateTestAutoDiscoveryCommand(),
                TestGrpcPipesCommand.CreateTestGrpcPipesCommand(),
                TestUefsConnectionCommand.CreateTestUefsConnectionCommand(),
                CreateGitHubReleaseCommand.CreateCreateGitHubReleaseCommand(),
                RunDynamicReentrantTaskCommand.CreateRunDynamicReentrantTaskCommand(),
                GenerateJsonSchemaCommand.CreateGenerateJsonSchemaCommand(),
                RemoveStalePrecompiledHeadersCommand.CreateRemoveStalePrecompiledHeadersCommand(),
                InstallPlatformSdkCommand.CreateInstallPlatformSdkCommand(),
                RunDriveMappedProcessCommand.CreateRunDriveMappedProcessCommand(),
                CreateJunctionCommand.CreateCreateJunctionCommand(),
                StressTestProcessCommand.CreateStressTestProcessCommand(),
                TestPduCommand.CreateTestPduCommand(),
                RunRemoteHostCommand.CreateRunRemoteHostCommand(),
                RunRemoteCommand.CreateRunRemoteCommand(),
                InspectSnmpCommand.CreateInspectSnmpCommand(),
                ReparentAdditionalPropertiesInTargetsCommand.CreateReparentAdditionalPropertiesInTargetsCommand(),
                WakeOnLanCommand.CreateWakeOnLanCommand(),
                TestUbaCommand.CreateTestUbaCommand(),
                RemoteZfsServerCommand.CreateRemoteZfsServerCommand(),
                RemoteZfsTestCommand.CreateRemoteZfsTestCommand(),
                EngineCheckoutCommand.CreateEngineCheckoutCommand(),
            };

            var command = new Command("internal", "Internal commands used by UET when it needs to call back into itself.");
            command.IsHidden = true;
            foreach (var subcommand in subcommands)
            {
                globalCommands.Add(subcommand);
                command.AddCommand(subcommand);
            }
            globalCommands.Add(command);
            return command;
        }
    }
}
