namespace B2Net.Models {
    using System.Collections.Generic;

    public class B2BucketOptions {
		public BucketTypes BucketType { get; set; } = BucketTypes.allPrivate;
		public int CacheControl { get; set; }
		public List<B2BucketLifecycleRule> LifecycleRules { get; set; }
		public List<B2CORSRule> CORSRules { get; set; }
	}
}
