namespace UET.Commands.Internal
{
    using System.CommandLine;
    using UET.Commands.Internal.CIBuild;
    using UET.Commands.Internal.CopyAndMutateBuildCs;
    using UET.Commands.Internal.RunAutomationTestFromBuildGraph;
    using UET.Commands.Internal.RunDownstreamTest;
    using UET.Commands.Internal.RunGauntletTestFromBuildGraph;
    using UET.Commands.Internal.SetFilterFile;
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
                RunAutomationTestFromBuildGraphCommand.CreateRunAutomationTestFromBuildGraphCommand(),
                RunDownstreamTestCommand.CreateRunDownstreamTestCommand(),
                RunGauntletTestFromBuildGraphCommand.CreateRunGauntletCommand(),
                SetFilterFileCommand.CreateSetFilterFileCommand(),
                UpdateCopyrightHeadersForMarketplaceCommand.CreateUpdateCopyrightHeadersForMarketplaceCommand(),
                UpdateUPluginCommand.CreateUpdateUPluginCommand(),
                UploadToBackblazeB2Command.CreateUploadToBackblazeB2Command(),
                TestGrpcPipesCommand.CreateTestGrpcPipesCommand(),
                TestUefsConnectionCommand.CreateTestUefsConnectionCommand(),
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
