namespace UET.Commands.Uefs
{
    using Redpoint.CommandLine;
    using Redpoint.Uefs.Commands.Build;
    using Redpoint.Uefs.Commands.Hash;
    using Redpoint.Uefs.Commands.List;
    using Redpoint.Uefs.Commands.Login;
    using Redpoint.Uefs.Commands.Mount;
    using Redpoint.Uefs.Commands.Pull;
    using Redpoint.Uefs.Commands.Push;
    using Redpoint.Uefs.Commands.Unmount;
    using Redpoint.Uefs.Commands.Verify;
    using Redpoint.Uefs.Commands.Wait;
    using System.CommandLine;

    internal sealed class UefsCommand : ICommandDescriptorProvider<UetGlobalCommandContext>
    {
        public static CommandDescriptor<UetGlobalCommandContext> Descriptor => UetCommandDescriptor.NewBuilder()
            .WithCommand(
                builder =>
                {
                    builder.AddCommand<UefsInstallCommand>();
                    builder.AddCommand<UefsUninstallCommand>();
                    builder.AddCommandWithoutGlobalContext<BuildCommand>();
                    builder.AddCommandWithoutGlobalContext<MountCommand>();
                    builder.AddCommandWithoutGlobalContext<UnmountCommand>();
                    builder.AddCommandWithoutGlobalContext<ListCommand>();
                    builder.AddCommandWithoutGlobalContext<PushCommand>();
                    builder.AddCommandWithoutGlobalContext<HashCommand>();
                    builder.AddCommandWithoutGlobalContext<PullCommand>();
                    builder.AddCommandWithoutGlobalContext<WaitCommand>();
                    builder.AddCommandWithoutGlobalContext<VerifyCommand>();
                    builder.AddCommandWithoutGlobalContext<LoginCommand>();

                    return new Command("uefs", "Run a UEFS command from UET.");
                })
            .Build();
    }
}
