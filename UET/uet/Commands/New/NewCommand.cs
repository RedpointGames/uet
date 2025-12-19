namespace UET.Commands.New
{
    using Redpoint.CommandLine;
    using System;
    using System.Collections.Generic;
    using System.CommandLine;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using UET.Commands.New.Plugin;

    internal sealed class NewCommand : ICommandDescriptorProvider<UetGlobalCommandContext>
    {
        public static CommandDescriptor<UetGlobalCommandContext> Descriptor => UetCommandDescriptor.NewBuilder()
            .WithCommand(
                builder =>
                {
                    builder.AddCommand<NewPluginCommand>();

                    return new Command("new", "Create a new Unreal Engine plugin or project. (experimental)")
                    {
                        IsHidden = true
                    };
                })
            .Build();
    }
}
