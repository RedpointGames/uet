namespace UET.Commands.Build
{
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.CommandLine;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Text.RegularExpressions;

    internal sealed class BuildCommand : ICommandDescriptorProvider<UetGlobalCommandContext>
    {
        public static CommandDescriptor<UetGlobalCommandContext> Descriptor => UetCommandDescriptor.NewBuilder()
            .WithOptions<BuildCommandOptions>()
            .WithInstance<BuildCommandInstance>()
            .WithCommand(
                builder =>
                {
                    var command = new Command("build", "Build an Unreal Engine project or plugin.");
                    builder.GlobalContext.CommandRequiresUetVersionInBuildConfig(command);
                    return command;
                })
            .WithRuntimeServices(
                (_, services, _) =>
                {
                    services.AddSingleton<IBuildSpecificationGenerator, DefaultBuildSpecificationGenerator>();
                })
            .Build();
    }
}
