namespace B2Net {
    using B2Net.Models;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IBuckets {
		Task<B2Bucket> Create(string bucketName, B2BucketOptions options, CancellationToken cancelToken = default(CancellationToken));
		Task<B2Bucket> Create(string bucketName, BucketTypes bucketType, CancellationToken cancelToken = default(CancellationToken));
		Task<B2Bucket> Delete(string bucketId = "", CancellationToken cancelToken = default(CancellationToken));
		Task<List<B2Bucket>> GetList(CancellationToken cancelToken = default(CancellationToken));
		Task<B2Bucket> Update(BucketTypes bucketType, string bucketId = "", CancellationToken cancelToken = default(CancellationToken));
		Task<B2Bucket> Update(B2BucketOptions options, string bucketId = "", CancellationToken cancelToken = default(CancellationToken));
		Task<B2Bucket> Update(B2BucketOptions options, int revisionNumber, string bucketId = "", CancellationToken cancelToken = default(CancellationToken));
	}
}
