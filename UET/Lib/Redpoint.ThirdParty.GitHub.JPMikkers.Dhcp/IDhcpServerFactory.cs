namespace GitHub.JPMikkers.Dhcp;

public interface IDhcpServerFactory
{
    IDhcpServer Create(IDhcpClientPersistentStore? persistentStore = null);
}
