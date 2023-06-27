namespace B2Net.Http.RequestGenerators {
    using B2Net.Models;
    using System;
    using System.Net.Http;
    using System.Net.Http.Headers;

    public static class BaseRequestGenerator {
		public static HttpRequestMessage PostRequest(string endpoint, string body, B2Options options) {
			var uri = new Uri(options.ApiUrl + "/b2api/" + Constants.Version + "/" + endpoint);
			var request = new HttpRequestMessage() {
				Method = HttpMethod.Post,
				RequestUri = uri,
				Content = new StringContent(body)
			};

			request.Headers.TryAddWithoutValidation("Authorization", options.AuthorizationToken);

			return request;
		}

		public static HttpRequestMessage PostRequestJson(string endpoint, string body, B2Options options) {
			var uri = new Uri(options.ApiUrl + "/b2api/" + Constants.Version + "/" + endpoint);
			var request = new HttpRequestMessage() {
				Method = HttpMethod.Post,
				RequestUri = uri,
				Content = new StringContent(body)
			};

			request.Headers.TryAddWithoutValidation("Authorization", options.AuthorizationToken);

			request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
			request.Content.Headers.ContentLength = body.Length;

			return request;
		}
	}
}
