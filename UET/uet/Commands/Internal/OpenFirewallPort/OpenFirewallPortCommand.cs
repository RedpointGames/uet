namespace UET.Commands.Internal.OpenFirewallPort
{
    using Microsoft.Extensions.Logging;
    using Redpoint.CommandLine;
    using Redpoint.PathResolution;
    using Redpoint.ProcessExecution;
    using System;
    using System.Collections.Generic;
    using System.CommandLine;
    using System.Text;

    internal class OpenFirewallPortCommand : ICommandDescriptorProvider<UetGlobalCommandContext>
    {
        public static CommandDescriptor<UetGlobalCommandContext> Descriptor => UetCommandDescriptor.NewBuilder()
            .WithOptions<Options>()
            .WithInstance<OpenFirewallPortCommandInstance>()
            .WithCommand(
                builder =>
                {
                    return new Command("open-firewall-port", "Open a firewall port on the Windows Firewall.");
                })
            .Build();

        internal sealed class Options
        {
            public Option<int> Port = new("--port");
        }

        private sealed class OpenFirewallPortCommandInstance : ICommandInstance
        {
            private readonly ILogger<OpenFirewallPortCommandInstance> _logger;
            private readonly IProcessExecutor _processExecutor;
            private readonly IPathResolver _pathResolver;
            private readonly Options _options;

            public OpenFirewallPortCommandInstance(
                ILogger<OpenFirewallPortCommandInstance> logger,
                IProcessExecutor processExecutor,
                IPathResolver pathResolver,
                Options options)
            {
                _logger = logger;
                _processExecutor = processExecutor;
                _pathResolver = pathResolver;
                _options = options;
            }

            public async Task<int> ExecuteAsync(ICommandInvocationContext context)
            {
                if (!OperatingSystem.IsWindows())
                {
                    return 0;
                }

                var pwsh = await _pathResolver.ResolveBinaryPath("pwsh").ConfigureAwait(false);

                var port = context.ParseResult.GetValueForOption(_options.Port);

                var script =
                    $$"""
                    $ErrorActionPreference = "Stop";

                    $TargetPort = {{port}};
                    $FirewallRuleName = "Port-$TargetPort";

                    $FirewallRule = (Get-NetFirewallRule -Name $FirewallRuleName -ErrorAction SilentlyContinue);
                    if ($null -ne $FirewallRule) 
                    {
                        $FirewallRulePortFilter = (Get-NetFirewallPortFilter -AssociatedNetFirewallRule $FirewallRule -ErrorAction SilentlyContinue);
                        $FirewallRuleAddressFilter = (Get-NetFirewallAddressFilter -AssociatedNetFirewallRule $FirewallRule -ErrorAction SilentlyContinue);
                        if ($null -eq $FirewallRulePortFilter -or
                            $null -eq $FirewallRuleAddressFilter -or
                            $FirewallRulePortFilter.LocalPort -ne $TargetPort -or
                            $FirewallRulePortFilter.Protocol -ne "TCP" -or
                            $FirewallRuleAddressFilter.RemoteAddress -ne "LocalSubnet" -or
                            $FirewallRule.Action -ne "Allow" -or
                            $FirewallRule.Direction -ne "Inbound") 
                        {
                            Write-Host "Removing stale firewall rule...";
                            Remove-NetFirewallRule -Name $FirewallRuleName;
                            $FirewallRule = $null;
                        }
                    }
                    if ($null -eq $FirewallRule)
                    {
                        Write-Host "Creating firewall rule to open port...";
                        $FirewallRule = (New-NetFirewallRule -Name $FirewallRuleName -DisplayName $FirewallRuleName -Direction Inbound -LocalPort $TargetPort -Protocol TCP -RemoteAddress LocalSubnet -Action Allow);
                    }
                    else
                    {
                        Write-Host "Port is already open via existing firewall rule.";
                    }
                    exit 0;
                    """;
                var encodedScript = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));

                await _processExecutor.ExecuteAsync(
                    new ProcessSpecification
                    {
                        FilePath = pwsh,
                        Arguments = [
                            "-NonInteractive",
                            "-OutputFormat",
                            "Text",
                            "-EncodedCommand",
                            encodedScript,
                        ]
                    },
                    CaptureSpecification.Passthrough,
                    context.GetCancellationToken()).ConfigureAwait(false);
                return 0;
            }
        }
    }
}
