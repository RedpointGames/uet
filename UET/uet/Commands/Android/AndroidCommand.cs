namespace UET.Commands.Android
{
    using Redpoint.CommandLine;
    using System.CommandLine;

    internal sealed class AndroidCommand : ICommandDescriptorProvider<UetGlobalCommandContext>
    {
        public static CommandDescriptor<UetGlobalCommandContext> Descriptor => UetCommandDescriptor.NewBuilder()
            .WithCommand(
                builder =>
                {
                    builder.AddCommand<AndroidKeepWirelessEnabledCommand>();

                    var command = new Command("android", "Various utilities for Android development.");
                    builder.GlobalContext.CommandRequiresUetVersionInBuildConfig(command);
                    return command;
                })
            .Build();
    }
}
