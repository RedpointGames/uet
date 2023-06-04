namespace UET.Commands.Internal
{
    using System.CommandLine;
    using UET.Commands.Internal.CIBuild;
    using UET.Commands.Internal.CopyAndMutateBuildCs;
    using UET.Commands.Internal.RunAutomationTestFromBuildGraph;
    using UET.Commands.Internal.RunDownstreamTest;
    using UET.Commands.Internal.RunGauntletTestFromBuildGraph;
    using UET.Commands.Internal.SetFilterFile;
    using UET.Commands.Internal.UpdateCopyrightHeadersForMarketplace;
    using UET.Commands.Internal.UpdateUPlugin;
    using UET.Commands.Internal.UploadToBackblazeB2;

    internal class InternalCommand
    {
        public static Command CreateInternalCommand()
        {
            var command = new Command("internal", "Internal commands used by UET when it needs to call back into itself.");
            command.IsHidden = true;
            command.AddCommand(CIBuildCommand.CreateCIBuildCommand());
            command.AddCommand(CopyAndMutateBuildCsCommand.CreateCopyAndMutateBuildCsCommand());
            command.AddCommand(RunAutomationTestFromBuildGraphCommand.CreateRunAutomationTestFromBuildGraphCommand());
            command.AddCommand(RunDownstreamTestCommand.CreateRunDownstreamTestCommand());
            command.AddCommand(RunGauntletTestFromBuildGraphCommand.CreateRunGauntletCommand());
            command.AddCommand(SetFilterFileCommand.CreateSetFilterFileCommand());
            command.AddCommand(UpdateCopyrightHeadersForMarketplaceCommand.CreateUpdateCopyrightHeadersForMarketplaceCommand());
            command.AddCommand(UpdateUPluginCommand.CreateUpdateUPluginCommand());
            command.AddCommand(UploadToBackblazeB2Command.CreateUploadToBackblazeB2Command());
            return command;
        }
    }
}
