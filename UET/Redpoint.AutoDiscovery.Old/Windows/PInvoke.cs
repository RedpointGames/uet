namespace Redpoint.AutoDiscovery.Windows
{
    using System.Runtime.InteropServices;
    using System.Runtime.Versioning;

    internal class PInvoke
    {
        [DllImport("DNSAPI.dll", ExactSpelling = true, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [SupportedOSPlatform("windows10.0.10240")]
        public static extern unsafe uint DnsServiceRegister(
            DNS_SERVICE_REGISTER_REQUEST* Request,
            [Optional] DNS_SERVICE_CANCEL* Cancel);

        [DllImport("DNSAPI.dll", ExactSpelling = true, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [SupportedOSPlatform("windows10.0.10240")]
        public static extern unsafe uint DnsServiceDeRegister(
            DNS_SERVICE_REGISTER_REQUEST* Request,
            [Optional] DNS_SERVICE_CANCEL* Cancel);

        public const uint DNS_REQUEST_PENDING = 9506;
    }
}
