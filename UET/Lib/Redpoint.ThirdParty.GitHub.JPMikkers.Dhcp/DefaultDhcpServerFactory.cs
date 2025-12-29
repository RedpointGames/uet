using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;

namespace GitHub.JPMikkers.Dhcp;

internal class DefaultDhcpServerFactory : IDhcpServerFactory
{
    private readonly IServiceProvider _serviceProvider;

    public DefaultDhcpServerFactory(
        IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public IDhcpServer Create(IDhcpClientPersistentStore? persistentStore = null)
    {
        return new DefaultDhcpServer(
            _serviceProvider.GetRequiredService<ILogger<DefaultDhcpServer>>(),
            _serviceProvider.GetRequiredService<IUdpSocketFactory>(),
            persistentStore);
    }
}
