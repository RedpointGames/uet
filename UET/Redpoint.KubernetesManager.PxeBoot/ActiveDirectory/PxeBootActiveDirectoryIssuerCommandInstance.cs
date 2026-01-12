namespace Redpoint.KubernetesManager.PxeBoot.ActiveDirectory
{
    using Microsoft.Extensions.Logging;
    using Redpoint.CommandLine;
    using Redpoint.KubernetesManager.HostedService;
    using Redpoint.KubernetesManager.PxeBoot.SelfLocation;
    using Redpoint.ServiceControl;
    using System.Threading.Tasks;

    internal class PxeBootActiveDirectoryIssuerCommandInstance : ICommandInstance
    {
        private readonly ILogger<PxeBootActiveDirectoryIssuerCommandInstance> _logger;
        private readonly IServiceControl _serviceControl;
        private readonly PxeBootActiveDirectoryIssuerOptions _options;
        private readonly IHostedServiceFromExecutable _hostedServiceFromExecutable;

        public PxeBootActiveDirectoryIssuerCommandInstance(
            ILogger<PxeBootActiveDirectoryIssuerCommandInstance> logger,
            IServiceControl serviceControl,
            PxeBootActiveDirectoryIssuerOptions options,
            IHostedServiceFromExecutable hostedServiceFromExecutable)
        {
            _logger = logger;
            _serviceControl = serviceControl;
            _options = options;
            _hostedServiceFromExecutable = hostedServiceFromExecutable;
        }

        private const string _serviceName = "rkm-ad-issuer";

        public async Task<int> ExecuteAsync(ICommandInvocationContext context)
        {
            if (context.ParseResult.GetValueForOption(_options.Install))
            {
                if (await _serviceControl.IsServiceInstalled(_serviceName))
                {
                    if (await _serviceControl.IsServiceRunning(_serviceName, context.GetCancellationToken()))
                    {
                        _logger.LogInformation($"Stopping service {_serviceName}...");
                        await _serviceControl.StopService(
                            _serviceName,
                            context.GetCancellationToken());
                    }

                    _logger.LogInformation($"Uninstalling service {_serviceName}...");
                    await _serviceControl.UninstallService(
                        _serviceName);
                }

                Directory.CreateDirectory(@"C:\ProgramData\RKM-AdIssuer");
                File.Copy(
                    PxeBootSelfLocation.GetSelfLocation(),
                    @"C:\ProgramData\RKM-AdIssuer\uet.exe",
                    true);

                _logger.LogInformation($"Installing service {_serviceName}...");
                await _serviceControl.InstallService(
                    _serviceName,
                    "RKM - Active Directory Issuer",
                    $@"C:\ProgramData\RKM-AdIssuer\uet.exe internal pxeboot active-directory-issuer --domain {context.ParseResult.GetValueForOption(_options.Domain)} --provisioner-api-address {context.ParseResult.GetValueForOption(_options.ProvisionerApiAddress)}",
                    username: context.ParseResult.GetValueForOption(_options.Username),
                    password: context.ParseResult.GetValueForOption(_options.Password));

                _logger.LogInformation($"Starting service {_serviceName}...");
                await _serviceControl.StartService(
                    _serviceName,
                    context.GetCancellationToken());
            }
            else
            {
                _logger.LogInformation("Monitoring for Active Directory join requests...");
                await _hostedServiceFromExecutable.RunHostedServicesAsync(context.GetCancellationToken());
            }
            return 0;
        }
    }
}
