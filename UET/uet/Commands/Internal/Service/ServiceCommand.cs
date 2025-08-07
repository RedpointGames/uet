namespace UET.Commands.Internal.Service
{
    using System;
    using System.Collections.Generic;
    using System.CommandLine;
    using System.Linq;
    using System.Text;
    using UET.Commands.Cluster;

    internal class ServiceCommand
    {
        public static Command CreateServiceCommand()
        {
            var command = new Command("service", "Manage services.");
            command.AddCommand(ServiceStartCommand.CreateServiceStartCommand());
            command.AddCommand(ServiceStopCommand.CreateServiceStopCommand());
            return command;
        }
    }
}
