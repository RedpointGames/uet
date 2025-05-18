using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Redpoint.Windows.HostNetworkingService
{
    public class HnsNotAvailableException : Exception
    {
        public HnsNotAvailableException()
            : base("The HNS API is not available. This is likely because the Windows Containers feature is not installed.")
        {
        }
    }
}
