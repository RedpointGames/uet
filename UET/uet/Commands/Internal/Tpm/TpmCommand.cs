namespace UET.Commands.Internal.Tpm
{
    using System;
    using System.Collections.Generic;
    using System.CommandLine;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    internal sealed class TpmCommand
    {
        public static Command CreateTpmCommand()
        {
            var command = new Command(
                "tpm",
                "Perform operations with the TPM.");
            command.AddCommand(TpmCreateAikCommand.CreateTpmCreateAikCommand());
            return command;
        }
    }
}
