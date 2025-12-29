namespace GitHub.JPMikkers.Dhcp
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public interface IDhcpClientPersistentStore
    {
        DhcpClientInformation Read();

        void Write(DhcpClientInformation dhcpClientInformation);
    }
}
