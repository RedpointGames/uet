namespace B2Net.Models {
	public class B2Capabilities {
		public B2Capabilities(B2AuthCapabilities authCapabilities) {
			BucketId = authCapabilities.bucketId;
			BucketName = authCapabilities.bucketName;
			Capabilities = authCapabilities.capabilities;
			NamePrefix = authCapabilities.namePrefix;
		}

		public string BucketName { get; }
		public string BucketId { get; }
		public string NamePrefix { get; }
		public string[] Capabilities { get; }
	}
}
