namespace UET.Commands.Internal.WindowsImaging
{
    using System;
    using System.Collections.Generic;
    using System.CommandLine;
    using System.Linq;
    using System.Text;
    using UET.Commands.Cluster;
    using UET.Commands.Internal.RunRemote;

    internal class WindowsImagingCommand
    {
        public static Command CreateWindowsImagingCommand()
        {
            var command = new Command("windows-imaging", "Create various boot images for Windows clients.");
            command.AddCommand(WindowsImagingCreatePxeBootCommand.CreateWindowsImagingCreatePxeBootCommand());
            return command;
        }
    }
}
