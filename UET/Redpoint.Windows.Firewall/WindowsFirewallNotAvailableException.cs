using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Redpoint.Windows.Firewall
{
    public class WindowsFirewallNotAvailableException : Exception
    {
        public WindowsFirewallNotAvailableException()
            : base("The Windows Firewall API is not available.")
        {
        }
    }
}
