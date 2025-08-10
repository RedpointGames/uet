namespace UET.Commands.Cluster
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Redpoint.ProcessExecution;
    using Redpoint.Registry;
    using Redpoint.ServiceControl;
    using System;
    using System.Collections.Generic;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Linq;
    using System.Runtime.Versioning;
    using System.Security.Principal;
    using System.Text;
    using System.Threading.Tasks;
    using UET.Services;

    [SupportedOSPlatform("windows5.0")]
    internal class ClusterSwitchVfpModeCommand
    {
        internal sealed class Options
        {
            public Option<bool> Filter = new Option<bool>("--filter", "Switch the VFP extension into filtering mode.");
            public Option<bool> Forward = new Option<bool>("--forward", "Switch the VFP extension into forwarding mode.");
            public Option<bool> Verify = new Option<bool>("--verify", "If set, this command verifies if the machine is in the target mode instead of changing the mode.");
        }

        public static Command CreateClusterSwitchVfpModeCommand()
        {
            var options = new Options();
            var command = new Command(
                "switch-vfp-mode",
                "Change the mode of the VFP network extension on Windows.")
            {
                FullDescription =
                """
                This command changes the mode of the VFP network extension on Windows. The VFP network extension must be in 'forwarding' mode for Windows container networking to work under Kubernetes, but Windows client SKUs have it set to the more limited 'filtering' mode by default.

                After changing the mode with this command, the computer must be restarted for the changes to take effect.

                This command must be run as an Administrator.
                """
            };
            command.AddAllOptions(options);
            command.AddCommonHandler<ClusterSwitchVfpModeCommandInstance>(options);
            return command;
        }

        private sealed class ClusterSwitchVfpModeCommandInstance : ICommandInstance
        {
            private readonly IProcessExecutor _processExecutor;
            private readonly ISelfLocation _selfLocation;
            private readonly IServiceControl _serviceControl;
            private readonly ILogger<ClusterSwitchVfpModeCommandInstance> _logger;
            private readonly Options _options;

            public ClusterSwitchVfpModeCommandInstance(
                IProcessExecutor processExecutor,
                ISelfLocation selfLocation,
                IServiceControl serviceControl,
                ILogger<ClusterSwitchVfpModeCommandInstance> logger,
                Options options)
            {
                _processExecutor = processExecutor;
                _selfLocation = selfLocation;
                _serviceControl = serviceControl;
                _logger = logger;
                _options = options;
            }

            private const string _envRunningUnderTrustedInstaller = "UET_SWITCH_VFP_MODE_RUNNING_UNDER_TRUSTEDINSTALLER";

            public async Task<int> ExecuteAsync(InvocationContext context)
            {
                if (!new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator))
                {
                    _logger.LogError("This command must be run as an Administrator.");
                    return 1;
                }

                var isFilter = context.ParseResult.GetValueForOption(_options.Filter);
                var isForward = context.ParseResult.GetValueForOption(_options.Forward);
                var verify = context.ParseResult.GetValueForOption(_options.Verify);
                if (!isFilter && !isForward)
                {
                    _logger.LogError("Expected either --filter or --forward.");
                    return 1;
                }

                if (Environment.GetEnvironmentVariable(_envRunningUnderTrustedInstaller) != "1")
                {
                    await _serviceControl.StartService(
                        "TrustedInstaller",
                        CancellationToken.None);

                    var args = new List<LogicalProcessArgument>
                    {
                        "cluster",
                        "switch-vfp-mode",
                        isFilter ? "--filter" : "--forward",
                    };
                    if (verify)
                    {
                        args.Add("--verify");
                    }

                    var exitCode = await _processExecutor.ExecuteAsync(
                        new ProcessSpecification
                        {
                            FilePath = _selfLocation.GetUetLocalLocation(false),
                            Arguments = args,
                            RunAsTrustedInstaller = true,
                            EnvironmentVariables = new Dictionary<string, string>
                            {
                                { _envRunningUnderTrustedInstaller, "1" }
                            }
                        },
                        CaptureSpecification.Passthrough,
                        context.GetCancellationToken());

                    var requestedMode = isFilter ? "filtering" : "forwarding";

                    if (exitCode == 10)
                    {
                        _logger.LogInformation($"VFP extension is already in '{requestedMode}' mode.");
                    }
                    else if (exitCode == 11)
                    {
                        _logger.LogWarning($"VFP extension is NOT in '{requestedMode}' mode.");
                    }
                    else if (exitCode == 12)
                    {
                        _logger.LogInformation($"Applying changes to registry to change VFP extension to '{requestedMode}' mode...");
                        _logger.LogInformation("Changes have been applied to the registry. The computer must now be restarted.");
                    }
                    else
                    {
                        _logger.LogError($"Got unexpected exit code from VFP extension mode call: {exitCode}");
                    }

                    return exitCode;
                }

                var targetMode = isFilter ? _filterMode : _forwardMode;

                // Verify if we're in the target mode already.
                var isInTargetMode = true;
                foreach (var key in targetMode)
                {
                    var stack = RegistryStack.OpenPath(key.Key, false, false);
                    if (!stack.Exists)
                    {
                        _logger.LogInformation($"Registry key does not exist: {key.Key}");
                        isInTargetMode = false;
                        break;
                    }

                    foreach (var value in key.Value)
                    {
                        var valueNames = stack.Key.GetValueNames();
                        if (!valueNames.Contains(value.Key))
                        {
                            _logger.LogInformation($"Registry value does not exist: {key.Key} -> {value.Key}");
                            isInTargetMode = false;
                            break;
                        }

                        switch (value.Value)
                        {
                            case int data:
                                if (stack.Key.GetValueKind(value.Key) != Microsoft.Win32.RegistryValueKind.DWord ||
                                    !(stack.Key.GetValue(value.Key) is int i) ||
                                    i != data)
                                {
                                    isInTargetMode = false;
                                }
                                break;
                            case string data:
                                if (stack.Key.GetValueKind(value.Key) != Microsoft.Win32.RegistryValueKind.String ||
                                    !(stack.Key.GetValue(value.Key) is string s) ||
                                    s != data)
                                {
                                    isInTargetMode = false;
                                }
                                break;
                            case RegistryBinaryValue data:
                                {
                                    var custom = CustomRegistryType.GetValue(stack.Key, value.Key);
                                    if (custom.type != data.Type ||
                                        !custom.data.SequenceEqual(data.Bytes))
                                    {
                                        isInTargetMode = false;
                                    }
                                    // Console.WriteLine($"{custom.type:X} {(string.Join(",", custom.data.Select(x => $"0x{x:X}")))}");
                                }
                                break;
                        }
                        if (!isInTargetMode)
                        {
                            break;
                        }
                    }
                }

                if (isInTargetMode)
                {
                    _logger.LogInformation("VFP extension is already in the requested mode.");
                    return 10;
                }

                _logger.LogInformation("VFP extension is NOT in the requested mode.");

                if (verify)
                {
                    // We aren't in the correct mode and we're only verifying, so exit with exit code 1.
                    return 11;
                }

                _logger.LogInformation("Applying changes to registry to change VFP extension mode...");

                foreach (var key in targetMode)
                {
                    var stack = RegistryStack.OpenPath(key.Key, true, true);

                    foreach (var value in key.Value)
                    {
                        switch (value.Value)
                        {
                            case int data:
                                stack.Key.SetValue(value.Key, data);
                                break;
                            case string data:
                                stack.Key.SetValue(value.Key, data);
                                break;
                            case RegistryBinaryValue data:
                                CustomRegistryType.SetValue(stack.Key, value.Key, data.Type, data.Bytes);
                                break;
                        }
                    }
                }

                _logger.LogInformation("Changes have been applied to the registry. The computer must now be restarted.");
                return 12;
            }

            private class RegistryBinaryValue(uint type, byte[] bytes)
            {
                public uint Type { get; } = type;
                public byte[] Bytes { get; } = bytes;
            }

            private readonly Dictionary<string, Dictionary<string, object>> _forwardMode = new()
            {
                {
                    @"HKLM:\SYSTEM\CurrentControlSet\Control\NetworkSetup2\Filters\{F74F241B-440F-4433-BB28-00F89EAD20D8}\Kernel",
                    new()
                    {
                        { "Optional", 1 },
                        { "FilterClass", "ms_switch_forward" }
                    }
                },
                {
                    @"HKLM:\SYSTEM\CurrentControlSet\Control\NetworkSetup2\Filters\{F74F241B-440F-4433-BB28-00F89EAD20D8}\Properties\{a111f1ef-5923-47c0-9a68-d0bafb577901}\0014",
                     new()
                     {
                         { string.Empty, new RegistryBinaryValue(0xffff0007, [0x01,0x00,0x00,0x00]) }
                     }
                },
                {
                    @"HKLM:\SYSTEM\CurrentControlSet\Control\NetworkSetup2\Filters\{F74F241B-440F-4433-BB28-00F89EAD20D8}\Properties\{a111f1f0-5923-47c0-9a68-d0bafb577901}\0002",
                     new()
                     {
                         { string.Empty, new RegistryBinaryValue(0xffff0012, [0x6d,0x00,0x73,0x00,0x5f,0x00,0x77,0x00,0x69,0x00,0x6e,0x00,0x76,0x00,0x66,0x00,0x70,0x00,0x00,0x00]) }
                     }
                },
                {
                    @"HKLM:\SYSTEM\CurrentControlSet\Control\NetworkSetup2\Filters\{F74F241B-440F-4433-BB28-00F89EAD20D8}\Properties\{a111f1f0-5923-47c0-9a68-d0bafb577901}\0006",
                     new()
                     {
                         { string.Empty, new RegistryBinaryValue(0xffff0012, [0x4d,0x00,0x69,0x00,0x63,0x00,0x72,0x00,0x6f,0x00,0x73,0x00,0x6f,0x00,0x66,0x00,0x74,0x00,0x20,0x00,0x41,0x00,0x7a,0x00,0x75,0x00,0x72,0x00,0x65,0x00,0x20,0x00,0x56,0x00,0x46,0x00,0x50,0x00,0x20,0x00,0x53,0x00,0x77,0x00,0x69,0x00,0x74,0x00,0x63,0x00,0x68,0x00,0x20,0x00,0x45,0x00,0x78,0x00,0x74,0x00,0x65,0x00,0x6e,0x00,0x73,0x00,0x69,0x00,0x6f,0x00,0x6e,0x00,0x00,0x00]) }
                     }
                },
                {
                    @"HKLM:\SYSTEM\CurrentControlSet\Control\NetworkSetup2\Filters\{F74F241B-440F-4433-BB28-00F89EAD20D8}\Properties\{a111f1f0-5923-47c0-9a68-d0bafb577901}\0008",
                     new()
                     {
                         { string.Empty, new RegistryBinaryValue(0xffff0012, [0x4d,0x00,0x69,0x00,0x63,0x00,0x72,0x00,0x6f,0x00,0x73,0x00,0x6f,0x00,0x66,0x00,0x74,0x00,0x20,0x00,0x41,0x00,0x7a,0x00,0x75,0x00,0x72,0x00,0x65,0x00,0x20,0x00,0x56,0x00,0x46,0x00,0x50,0x00,0x20,0x00,0x53,0x00,0x77,0x00,0x69,0x00,0x74,0x00,0x63,0x00,0x68,0x00,0x20,0x00,0x45,0x00,0x78,0x00,0x74,0x00,0x65,0x00,0x6e,0x00,0x73,0x00,0x69,0x00,0x6f,0x00,0x6e,0x00,0x00,0x00]) }
                     }
                },
                {
                    @"HKLM:\SYSTEM\CurrentControlSet\Control\NetworkSetup2\Filters\{F74F241B-440F-4433-BB28-00F89EAD20D8}\Properties\{a111f1f0-5923-47c0-9a68-d0bafb577901}\0014",
                     new()
                     {
                         { string.Empty, new RegistryBinaryValue(0xffff0011, [0x01]) }
                     }
                },
                {
                    @"HKLM:\SYSTEM\CurrentControlSet\Control\NetworkSetup2\Filters\{F74F241B-440F-4433-BB28-00F89EAD20D8}\Properties\{a111f1f0-5923-47c0-9a68-d0bafb577901}\0016",
                     new()
                     {
                         { string.Empty, new RegistryBinaryValue(0xffff0011, [0x01]) }
                     }
                },
                {
                    @"HKLM:\SYSTEM\CurrentControlSet\Control\NetworkSetup2\Filters\{F74F241B-440F-4433-BB28-00F89EAD20D8}\Properties\{a111f1f0-5923-47c0-9a68-d0bafb577901}\005a",
                     new()
                     {
                         { string.Empty, new RegistryBinaryValue(0xffff0011, [0x01]) }
                     }
                },
                {
                    @"HKLM:\SYSTEM\CurrentControlSet\Control\NetworkSetup2\Filters\{F74F241B-440F-4433-BB28-00F89EAD20D8}\Properties\{a111f1f1-5923-47c0-9a68-d0bafb577901}\0006",
                     new()
                     {
                         { string.Empty, new RegistryBinaryValue(0xffff2012, [0x76,0x00,0x6d,0x00,0x6e,0x00,0x65,0x00,0x74,0x00,0x65,0x00,0x78,0x00,0x74,0x00,0x65,0x00,0x6e,0x00,0x73,0x00,0x69,0x00,0x6f,0x00,0x6e,0x00,0x00,0x00,0x00,0x00]) }
                     }
                },
                {
                    @"HKLM:\SYSTEM\CurrentControlSet\Control\NetworkSetup2\Filters\{F74F241B-440F-4433-BB28-00F89EAD20D8}\Properties\{a111f1f7-5923-47c0-9a68-d0bafb577901}\0002",
                     new()
                     {
                         { string.Empty, new RegistryBinaryValue(0xffff000d, [0x1b,0x24,0x4f,0xf7,0x0f,0x44,0x33,0x44,0xbb,0x28,0x00,0xf8,0x9e,0xad,0x20,0xd8]) }
                     }
                },
                {
                    @"HKLM:\SYSTEM\CurrentControlSet\Control\NetworkSetup2\Filters\{F74F241B-440F-4433-BB28-00F89EAD20D8}\Properties\{a111f1f7-5923-47c0-9a68-d0bafb577901}\0004",
                     new()
                     {
                         { string.Empty, new RegistryBinaryValue(0xffff0012, [0x6d,0x00,0x73,0x00,0x5f,0x00,0x73,0x00,0x77,0x00,0x69,0x00,0x74,0x00,0x63,0x00,0x68,0x00,0x5f,0x00,0x66,0x00,0x6f,0x00,0x72,0x00,0x77,0x00,0x61,0x00,0x72,0x00,0x64,0x00,0x00,0x00]) }
                     }
                },
                {
                    @"HKLM:\SYSTEM\CurrentControlSet\Control\NetworkSetup2\Filters\{F74F241B-440F-4433-BB28-00F89EAD20D8}\Properties\{a111f1f7-5923-47c0-9a68-d0bafb577901}\0006",
                     new()
                     {
                         { string.Empty, new RegistryBinaryValue(0xffff0011, [0x01]) }
                     }
                },
                {
                    @"HKLM:\SYSTEM\CurrentControlSet\Control\NetworkSetup2\Filters\{F74F241B-440F-4433-BB28-00F89EAD20D8}\Properties\{a111f200-5923-47c0-9a68-d0bafb577901}\0002",
                     new()
                     {
                         { string.Empty, new RegistryBinaryValue(0xffff0012, [0x76,0x00,0x66,0x00,0x70,0x00,0x65,0x00,0x78,0x00,0x74,0x00,0x2e,0x00,0x69,0x00,0x6e,0x00,0x66,0x00,0x00,0x00]) }
                     }
                },
                {
                    @"HKLM:\SYSTEM\CurrentControlSet\Control\NetworkSetup2\Filters\{F74F241B-440F-4433-BB28-00F89EAD20D8}\Properties\{a111f200-5923-47c0-9a68-d0bafb577901}\0004",
                     new()
                     {
                         { string.Empty, new RegistryBinaryValue(0xffff0012, [0x49,0x00,0x6e,0x00,0x73,0x00,0x74,0x00,0x61,0x00,0x6c,0x00,0x6c,0x00,0x00,0x00]) }
                     }
                },
                {
                    @"HKLM:\SYSTEM\CurrentControlSet\Control\NetworkSetup2\Filters\{F74F241B-440F-4433-BB28-00F89EAD20D8}\Properties\{a111f200-5923-47c0-9a68-d0bafb577901}\0028",
                     new()
                     {
                         { string.Empty, new RegistryBinaryValue(0xffff1003, [0xdd,0x07,0x0c,0x00,0x04,0x00,0x05,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00]) }
                     }
                }
            };

            private readonly Dictionary<string, Dictionary<string, object>> _filterMode = new()
            {
                {
                    @"HKLM:\SYSTEM\CurrentControlSet\Control\NetworkSetup2\Filters\{F74F241B-440F-4433-BB28-00F89EAD20D8}\Kernel",
                    new()
                    {
                        { "Optional", 1 },
                        { "FilterClass", "ms_switch_filter" }
                    }
                },
                {
                    @"HKLM:\SYSTEM\CurrentControlSet\Control\NetworkSetup2\Filters\{F74F241B-440F-4433-BB28-00F89EAD20D8}\Properties\{a111f1ef-5923-47c0-9a68-d0bafb577901}\0014",
                     new()
                     {
                         { string.Empty, new RegistryBinaryValue(0xffff0007, [0x01,0x00,0x00,0x00]) }
                     }
                },
                {
                    @"HKLM:\SYSTEM\CurrentControlSet\Control\NetworkSetup2\Filters\{F74F241B-440F-4433-BB28-00F89EAD20D8}\Properties\{a111f1f0-5923-47c0-9a68-d0bafb577901}\0002",
                     new()
                     {
                         { string.Empty, new RegistryBinaryValue(0xffff0012, [0x6d,0x00,0x73,0x00,0x5f,0x00,0x77,0x00,0x69,0x00,0x6e,0x00,0x76,0x00,0x66,0x00,0x70,0x00,0x00,0x00]) }
                     }
                },
                {
                    @"HKLM:\SYSTEM\CurrentControlSet\Control\NetworkSetup2\Filters\{F74F241B-440F-4433-BB28-00F89EAD20D8}\Properties\{a111f1f0-5923-47c0-9a68-d0bafb577901}\0006",
                     new()
                     {
                         { string.Empty, new RegistryBinaryValue(0xffff0012, [0x4d,0x00,0x69,0x00,0x63,0x00,0x72,0x00,0x6f,0x00,0x73,0x00,0x6f,0x00,0x66,0x00,0x74,0x00,0x20,0x00,0x41,0x00,0x7a,0x00,0x75,0x00,0x72,0x00,0x65,0x00,0x20,0x00,0x56,0x00,0x46,0x00,0x50,0x00,0x20,0x00,0x53,0x00,0x77,0x00,0x69,0x00,0x74,0x00,0x63,0x00,0x68,0x00,0x20,0x00,0x46,0x00,0x69,0x00,0x6c,0x00,0x74,0x00,0x65,0x00,0x72,0x00,0x20,0x00,0x45,0x00,0x78,0x00,0x74,0x00,0x65,0x00,0x6e,0x00,0x73,0x00,0x69,0x00,0x6f,0x00,0x6e,0x00,0x00,0x00]) }
                     }
                },
                {
                    @"HKLM:\SYSTEM\CurrentControlSet\Control\NetworkSetup2\Filters\{F74F241B-440F-4433-BB28-00F89EAD20D8}\Properties\{a111f1f0-5923-47c0-9a68-d0bafb577901}\0008",
                     new()
                     {
                         { string.Empty, new RegistryBinaryValue(0xffff0012, [0x4d,0x00,0x69,0x00,0x63,0x00,0x72,0x00,0x6f,0x00,0x73,0x00,0x6f,0x00,0x66,0x00,0x74,0x00,0x20,0x00,0x41,0x00,0x7a,0x00,0x75,0x00,0x72,0x00,0x65,0x00,0x20,0x00,0x56,0x00,0x46,0x00,0x50,0x00,0x20,0x00,0x53,0x00,0x77,0x00,0x69,0x00,0x74,0x00,0x63,0x00,0x68,0x00,0x20,0x00,0x46,0x00,0x69,0x00,0x6c,0x00,0x74,0x00,0x65,0x00,0x72,0x00,0x20,0x00,0x45,0x00,0x78,0x00,0x74,0x00,0x65,0x00,0x6e,0x00,0x73,0x00,0x69,0x00,0x6f,0x00,0x6e,0x00,0x00,0x00]) }
                     }
                },
                {
                    @"HKLM:\SYSTEM\CurrentControlSet\Control\NetworkSetup2\Filters\{F74F241B-440F-4433-BB28-00F89EAD20D8}\Properties\{a111f1f0-5923-47c0-9a68-d0bafb577901}\0014",
                     new()
                     {
                         { string.Empty, new RegistryBinaryValue(0xffff0011, [0x01]) }
                     }
                },
                {
                    @"HKLM:\SYSTEM\CurrentControlSet\Control\NetworkSetup2\Filters\{F74F241B-440F-4433-BB28-00F89EAD20D8}\Properties\{a111f1f0-5923-47c0-9a68-d0bafb577901}\0016",
                     new()
                     {
                         { string.Empty, new RegistryBinaryValue(0xffff0011, [0x01]) }
                     }
                },
                {
                    @"HKLM:\SYSTEM\CurrentControlSet\Control\NetworkSetup2\Filters\{F74F241B-440F-4433-BB28-00F89EAD20D8}\Properties\{a111f1f0-5923-47c0-9a68-d0bafb577901}\005a",
                     new()
                     {
                         { string.Empty, new RegistryBinaryValue(0xffff0011, [0x01]) }
                     }
                },
                {
                    @"HKLM:\SYSTEM\CurrentControlSet\Control\NetworkSetup2\Filters\{F74F241B-440F-4433-BB28-00F89EAD20D8}\Properties\{a111f1f1-5923-47c0-9a68-d0bafb577901}\0006",
                     new()
                     {
                         { string.Empty, new RegistryBinaryValue(0xffff2012, [0x76,0x00,0x6d,0x00,0x6e,0x00,0x65,0x00,0x74,0x00,0x65,0x00,0x78,0x00,0x74,0x00,0x65,0x00,0x6e,0x00,0x73,0x00,0x69,0x00,0x6f,0x00,0x6e,0x00,0x00,0x00,0x00,0x00]) }
                     }
                },
                {
                    @"HKLM:\SYSTEM\CurrentControlSet\Control\NetworkSetup2\Filters\{F74F241B-440F-4433-BB28-00F89EAD20D8}\Properties\{a111f1f7-5923-47c0-9a68-d0bafb577901}\0002",
                     new()
                     {
                         { string.Empty, new RegistryBinaryValue(0xffff000d, [0x1b,0x24,0x4f,0xf7,0x0f,0x44,0x33,0x44,0xbb,0x28,0x00,0xf8,0x9e,0xad,0x20,0xd8]) }
                     }
                },
                {
                    @"HKLM:\SYSTEM\CurrentControlSet\Control\NetworkSetup2\Filters\{F74F241B-440F-4433-BB28-00F89EAD20D8}\Properties\{a111f1f7-5923-47c0-9a68-d0bafb577901}\0004",
                     new()
                     {
                         { string.Empty, new RegistryBinaryValue(0xffff0012, [0x6d,0x00,0x73,0x00,0x5f,0x00,0x73,0x00,0x77,0x00,0x69,0x00,0x74,0x00,0x63,0x00,0x68,0x00,0x5f,0x00,0x66,0x00,0x69,0x00,0x6c,0x00,0x74,0x00,0x65,0x00,0x72,0x00,0x00,0x00]) }
                     }
                },
                {
                    @"HKLM:\SYSTEM\CurrentControlSet\Control\NetworkSetup2\Filters\{F74F241B-440F-4433-BB28-00F89EAD20D8}\Properties\{a111f1f7-5923-47c0-9a68-d0bafb577901}\0006",
                     new()
                     {
                         { string.Empty, new RegistryBinaryValue(0xffff0011, [0x01]) }
                     }
                },
                {
                    @"HKLM:\SYSTEM\CurrentControlSet\Control\NetworkSetup2\Filters\{F74F241B-440F-4433-BB28-00F89EAD20D8}\Properties\{a111f200-5923-47c0-9a68-d0bafb577901}\0002",
                     new()
                     {
                         { string.Empty, new RegistryBinaryValue(0xffff0012, [0x76,0x00,0x66,0x00,0x70,0x00,0x66,0x00,0x69,0x00,0x6c,0x00,0x74,0x00,0x65,0x00,0x72,0x00,0x2e,0x00,0x69,0x00,0x6e,0x00,0x66,0x00,0x00,0x00]) }
                     }
                },
                {
                    @"HKLM:\SYSTEM\CurrentControlSet\Control\NetworkSetup2\Filters\{F74F241B-440F-4433-BB28-00F89EAD20D8}\Properties\{a111f200-5923-47c0-9a68-d0bafb577901}\0004",
                     new()
                     {
                         { string.Empty, new RegistryBinaryValue(0xffff0012, [0x49,0x00,0x6e,0x00,0x73,0x00,0x74,0x00,0x61,0x00,0x6c,0x00,0x6c,0x00,0x00,0x00]) }
                     }
                },
                {
                    @"HKLM:\SYSTEM\CurrentControlSet\Control\NetworkSetup2\Filters\{F74F241B-440F-4433-BB28-00F89EAD20D8}\Properties\{a111f200-5923-47c0-9a68-d0bafb577901}\0028",
                     new()
                     {
                         { string.Empty, new RegistryBinaryValue(0xffff1003, [0xdd,0x07,0x0c,0x00,0x04,0x00,0x05,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00]) }
                     }
                },
            };
        }
    }
}
