namespace UET.Commands.OpenGE
{
    using System.CommandLine;

    internal class OpenGECommand
    {
        public static Command CreateOpenGECommand()
        {
            var rootCommand = new Command("openge", "Run an OpenGE command from UET, e.g. 'uet openge install'.");
            rootCommand.IsHidden = true;
            rootCommand.AddCommand(OpenGEInstallCommand.CreateInstallCommand());
            rootCommand.AddCommand(OpenGEUninstallCommand.CreateUninstallCommand());
            return rootCommand;
        }
    }
}
