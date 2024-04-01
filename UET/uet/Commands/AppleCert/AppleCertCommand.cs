namespace UET.Commands.AppleCert
{
    using System.CommandLine;

    internal sealed class AppleCertCommand
    {
        public static Command CreateAppleCertCommand()
        {
            var command = new Command("apple-cert", "Generate and export certificates for signing games on iOS.");
            command.AddCommand(AppleCertCreateCommand.CreateAppleCertCreateCommand());
            command.AddCommand(AppleCertFinalizeCommand.CreateAppleCertFinalizeCommand());
            return command;
        }
    }
}
