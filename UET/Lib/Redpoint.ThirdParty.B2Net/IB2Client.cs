namespace B2Net {
    using System.Threading;
    using System.Threading.Tasks;
    using B2Net.Models;

    public interface IB2Client {
		IBuckets Buckets { get; }
		IFiles Files { get; }
		ILargeFiles LargeFiles { get; }
		B2Capabilities Capabilities { get; }

		Task<B2Options> Authorize(CancellationToken cancelToken = default(CancellationToken));
	}
}
