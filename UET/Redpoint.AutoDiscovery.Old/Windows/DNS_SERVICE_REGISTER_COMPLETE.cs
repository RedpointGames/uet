namespace Redpoint.AutoDiscovery.Windows
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading.Tasks;

    [UnmanagedFunctionPointerAttribute(CallingConvention.Winapi)]
    internal unsafe delegate void DNS_SERVICE_REGISTER_COMPLETE(
        uint Status,
        void* QueryContext,
        DNS_SERVICE_INSTANCE* Instance);
}
