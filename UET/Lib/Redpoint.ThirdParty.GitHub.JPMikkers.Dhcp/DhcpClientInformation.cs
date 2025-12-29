using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;

namespace GitHub.JPMikkers.Dhcp;

public class DhcpClientInformation
{
    private List<DhcpClient> _clients = [];

    public DateTime TimeStamp
    {
        get
        {
            return DateTime.Now;
        }
        set
        {
        }
    }

    public List<DhcpClient> Clients
    {
        get
        {
            return _clients;
        }
        set
        {
            _clients = value;
        }
    }
}
