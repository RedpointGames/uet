namespace GitHub.JPMikkers.Dhcp;

public interface IDhcpMessageInterceptor
{
    void Apply(DhcpMessage sourceMsg, DhcpMessage targetMsg);
}