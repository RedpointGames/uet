namespace UET.Commands.Android
{
    using System.CommandLine;

    internal sealed class AndroidCommand
    {
        public static Command CreateAndroidCommand()
        {
            var command = new Command("android", "Various utilities for Android development.");
            command.AddCommand(AndroidKeepWirelessEnabledCommand.CreateAndroidKeepWirelessEnabledCommand());
            return command;
        }
    }
}
