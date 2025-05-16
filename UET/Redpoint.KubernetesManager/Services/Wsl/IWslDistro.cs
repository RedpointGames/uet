namespace Redpoint.KubernetesManager.Services.Windows
{
    using System.Net;
    using System.Runtime.Versioning;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    internal interface IWslDistro
    {
        [SupportedOSPlatform("windows")]
        string WslPath { get; }

        [SupportedOSPlatform("windows")]
        Task<string> GetWslDistroName(CancellationToken cancellationToken);

        [SupportedOSPlatform("windows")]
        Task<IPAddress?> GetWslDistroIPAddress(CancellationToken cancellationToken);

        [SupportedOSPlatform("windows")]
        Task<string?> GetWslDistroMACAddress(CancellationToken cancellationToken);

        [SupportedOSPlatform("windows")]
        Task<int> RunWslInvocation(string[] args, string input, Encoding encoding, CancellationToken cancellationToken, string? filename = null);

        [SupportedOSPlatform("windows")]
        Task<string> CaptureWslInvocation(string[] args, Encoding encoding, CancellationToken cancellationToken, string? filename = null);
    }
}