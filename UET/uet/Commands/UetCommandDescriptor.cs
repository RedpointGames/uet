namespace UET.Commands
{
    using Redpoint.CommandLine;
    using System.CommandLine.Invocation;

    internal class UetCommandDescriptor
    {
        public static CommandDescriptorBuilder<UetGlobalCommandContext> NewBuilder()
        {
            return CommandDescriptor<UetGlobalCommandContext>.NewBuilder();
        }
    }
}
