﻿using Microsoft.Extensions.Options;
using Redpoint.AutoDiscovery;
using Redpoint.Concurrency;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UET.Commands.Cluster
{
    internal sealed class ClusterCommand
    {
        public static Command CreateClusterCommand()
        {
            string activeFile;
            string rootDirectory;
            if (OperatingSystem.IsWindows())
            {
                activeFile = "C:\\RKM\\active";
                rootDirectory = "C:\\RKM";
            }
            else if (OperatingSystem.IsMacOS())
            {
                activeFile = "/Users/Shared/RKM/active";
                rootDirectory = "/Users/Shared/RKM";
            }
            else
            {
                activeFile = "/opt/rkm/active";
                rootDirectory = "/opt/rkm";
            }

            var command = new Command(
                "cluster",
                "Deploy Kubernetes clusters containing Windows, macOS and Linux nodes.");
            command.FullDescription =
                $"""
                Commands to deploy and manage Kubernetes clusters using RKM (the Redpoint Kubernetes Manager).

                Kubernetes clusters set up by this command are intended for build and automation workloads on trusted networks, with trusted code. They are not suitable for Internet-facing workloads, and actively trade security and isolation for ease-of-use and compatibility with the greatest range of platforms. If you are running Internet-facing workloads, please use something like 'k3s' instead.

                If you have no cluster set up on your network already, you must use 'uet cluster start' on a Linux machine; this machine will run the Kubernetes controller components that can only run on Linux. It will also join the cluster as a normal Kubernetes node capable of running workloads.

                After the cluster has been created, you can join Windows and Linux (and in the future, macOS) machines to the cluster using 'uet cluster start'.

                The file located at {activeFile} determines the current active installation. If that file does not exist, RKM will set up a new installation. You can use this file to switch between RKM installations by stopping the service, editing the contents of the file, and then starting the service again.
                
                You can always uninstall all Kubernetes state by stopping the service and then removing everything underneath {rootDirectory}.
                """;
            command.AddCommand(ClusterStartCommand.CreateClusterStartCommand());
            command.AddCommand(ClusterStopCommand.CreateClusterStopCommand());
            command.AddCommand(ClusterLogsCommand.CreateClusterLogsCommand());
            if (OperatingSystem.IsWindows())
            {
                command.AddCommand(ClusterGetHnsEndpointCommand.CreateClusterGetHnsEndpointCommand());
            }
            return command;
        }
    }
}
