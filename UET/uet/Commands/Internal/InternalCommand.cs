﻿namespace UET.Commands.Internal
{
    using System.CommandLine;
    using UET.Commands.Internal.CIBuild;
    using UET.Commands.Internal.CopyAndMutateBuildCs;
    using UET.Commands.Internal.CreateGitHubRelease;
    using UET.Commands.Internal.DynamicReentrantTask;
    using UET.Commands.Internal.GenerateJsonSchema;
    using UET.Commands.Internal.InstallPlatformSdk;
    using UET.Commands.Internal.OpenGE;
    using UET.Commands.Internal.RemoveStalePrecompiledHeaders;
    using UET.Commands.Internal.RunDownstreamTest;
    using UET.Commands.Internal.RunGauntletTestFromBuildGraph;
    using UET.Commands.Internal.SetFilterFile;
    using UET.Commands.Internal.SetupAppleTwoFactorProxy;
    using UET.Commands.Internal.TestGrpcPipes;
    using UET.Commands.Internal.TestUefsConnection;
    using UET.Commands.Internal.UpdateCopyrightHeadersForMarketplace;
    using UET.Commands.Internal.UpdateUPlugin;
    using UET.Commands.Internal.UploadToBackblazeB2;

    internal class InternalCommand
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
                UpdateCopyrightHeadersForMarketplaceCommand.CreateUpdateCopyrightHeadersForMarketplaceCommand(),
                UpdateUPluginCommand.CreateUpdateUPluginCommand(),
                UploadToBackblazeB2Command.CreateUploadToBackblazeB2Command(),
                TestGrpcPipesCommand.CreateTestGrpcPipesCommand(),
                TestUefsConnectionCommand.CreateTestUefsConnectionCommand(),
                CreateGitHubReleaseCommand.CreateCreateGitHubReleaseCommand(),
                RunDynamicReentrantTaskCommand.CreateRunDynamicReentrantTaskCommand(),
                GenerateJsonSchemaCommand.CreateGenerateJsonSchemaCommand(),
                RemoveStalePrecompiledHeadersCommand.CreateRemoveStalePrecompiledHeadersCommand(),
                InstallPlatformSdkCommand.CreateInstallPlatformSdkCommand(),
                OpenGECommand.CreateOpenGECommand(),
                SetupAppleTwoFactoryProxyCommand.CreateSetupAppleTwoFactoryProxyCommand(),
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
