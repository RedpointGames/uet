namespace UET.Commands.Uefs
{
    using System.CommandLine;
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

    internal static class UefsCommand
    {
        public static ICommandLineBuilder RegisterUefsCommand(this ICommandLineBuilder rootBuilder)
        {
            rootBuilder.AddUnhandledCommand(
                builder =>
                {
                    builder
                        .RegisterUefsInstallCommand()
                        .RegisterUefsUninstallCommand();
                    return new Command("uefs", "Run a UEFS command from UET.");
                });
            return rootBuilder;
        }

        public static Command CreateUefsCommand()
        {
            var rootCommand = new Command("uefs", "Run a UEFS command from UET.");
            //rootCommand.AddCommand(UefsInstallCommand.CreateInstallCommand());
            //rootCommand.AddCommand(UefsUninstallCommand.CreateUninstallCommand());
            rootCommand.AddCommand(BuildCommand.CreateBuildCommand());
            rootCommand.AddCommand(MountCommand.CreateMountCommand());
            rootCommand.AddCommand(UnmountCommand.CreateUnmountCommand());
            rootCommand.AddCommand(ListCommand.CreateListCommand());
            rootCommand.AddCommand(PushCommand.CreatePushCommand());
            rootCommand.AddCommand(HashCommand.CreateHashCommand());
            rootCommand.AddCommand(PullCommand.CreatePullCommand());
            rootCommand.AddCommand(WaitCommand.CreateWaitCommand());
            rootCommand.AddCommand(VerifyCommand.CreateVerifyCommand());
            rootCommand.AddCommand(LoginCommand.CreateLoginCommand());
            return rootCommand;
        }
    }
}
