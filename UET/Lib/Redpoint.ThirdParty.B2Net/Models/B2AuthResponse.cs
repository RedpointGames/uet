namespace B2Net.Models {
	public class B2AuthResponse {
		public string accountId { get; set; }
		public string apiUrl { get; set; }
		public string authorizationToken { get; set; }
		public string downloadUrl { get; set; }
		public long recommendedPartSize { get; set; }
		public long absoluteMinimumPartSize { get; set; }
		public long minimumPartSize { get; set; }
		public B2AuthCapabilities allowed { get; set; }
	}

	public class B2AuthCapabilities {
		public string bucketId { get; set; }
		public string bucketName { get; set; }
		public string namePrefix { get; set; }
		public string[] capabilities { get; set; }
	}
}
