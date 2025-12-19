namespace UET.Commands.AppleCert
{
    using Redpoint.CommandLine;
    using System.CommandLine;
    using UET.Commands.Android;

    internal sealed class AppleCertCommand : ICommandDescriptorProvider<UetGlobalCommandContext>
    {
        public static CommandDescriptor<UetGlobalCommandContext> Descriptor => UetCommandDescriptor.NewBuilder()
            .WithCommand(
                builder =>
                {
                    builder.AddCommand<AppleCertCreateCommand>();
                    builder.AddCommand<AppleCertFinalizeCommand>();

                    var command = new Command("apple-cert", "Generate and export certificates for signing games on iOS.");
                    builder.GlobalContext.CommandRequiresUetVersionInBuildConfig(command);
                    return command;
                })
            .Build();
    }
}
