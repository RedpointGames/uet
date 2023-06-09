namespace Redpoint.UET.Workspace.ParallelCopy
{
    using Grpc.Core.Logging;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading.Tasks;

    internal interface IParallelCopy
    {
        Task CopyAsync(CopyDescriptor descriptor, CancellationToken cancellationToken);
    }
}
