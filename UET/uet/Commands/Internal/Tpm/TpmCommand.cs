namespace UET.Commands.Internal.Tpm
{
    using Redpoint.CommandLine;
    using System;
    using System.Collections.Generic;
    using System.CommandLine;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    internal sealed class TpmCommand : ICommandDescriptorProvider<UetGlobalCommandContext>
    {
        public static CommandDescriptor<UetGlobalCommandContext> Descriptor => UetCommandDescriptor.NewBuilder()
            .WithCommand(
                builder =>
                {
                    builder.AddCommand<TpmCreateAikCommand>();

                    return new Command(
                        "tpm",
                        "Perform operations with the TPM.");
                })
            .Build();
    }
}
