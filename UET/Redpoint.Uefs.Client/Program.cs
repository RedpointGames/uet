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

var rootCommand = new RootCommand("The UEFS client allows you to build and mount UEFS packages.");
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

var exitCode = await rootCommand.InvokeAsync(args);
await Console.Out.FlushAsync();
await Console.Error.FlushAsync();
Environment.Exit(exitCode);
throw new BadImageFormatException();