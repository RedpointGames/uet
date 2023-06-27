namespace B2Net.Models {
    using System;

    public class AuthorizationException : Exception {
		public AuthorizationException(string response) : base("There was an error during authorization. See inner exception for details.", new Exception(response)) { }
	}
	public class NotAuthorizedException : Exception {
		public NotAuthorizedException(string message) : base(message) { }
	}
	public class CopyReplaceSetupException : Exception {
		public CopyReplaceSetupException(string message) : base(message) { }
	}

	public class B2Exception : Exception {
		public string Status { get; set; }
		public string Code { get; set; }
		public bool ShouldRetryRequest { get; set; }

		public B2Exception(string code, string status, string message, bool shouldRetry) : base(message) {
			Status = status;
			Code = code;
			ShouldRetryRequest = shouldRetry;
		}
	}
}
