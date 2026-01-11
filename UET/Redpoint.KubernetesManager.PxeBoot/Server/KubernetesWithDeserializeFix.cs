namespace Redpoint.KubernetesManager.PxeBoot.Server
{
    using k8s;
    using k8s.Autorest;
    using k8s.Models;
    using Microsoft.AspNetCore.Http;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text.Json;
    using System.Threading.Tasks;

    internal class KubernetesWithDeserializeFix : Kubernetes
    {
        public KubernetesWithDeserializeFix(KubernetesClientConfiguration config, params DelegatingHandler[] handlers) : base(config, handlers)
        {
        }

        protected override Task<HttpResponseMessage> SendRequest<T>(string relativeUri, HttpMethod method, IReadOnlyDictionary<string, IReadOnlyList<string>> customHeaders, T body, CancellationToken cancellationToken)
        {
            if (body == null || body is Eventsv1Event)
            {
                return base.SendRequest<T>(relativeUri, method, customHeaders, body, cancellationToken);
            }

            var httpRequest = new HttpRequestMessage
            {
                Method = method,
                RequestUri = new Uri(BaseUri, relativeUri),
            };
            httpRequest.Version = HttpVersion.Version20;

            // Set Headers
            if (customHeaders != null)
            {
                foreach (var header in customHeaders)
                {
                    httpRequest.Headers.Remove(header.Key);
                    httpRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }

            string requestContent;
            if (body is string str)
            {
                requestContent = str;
            }
            else if (body is JsonElement jsonElement)
            {
                using (var jsonStream = new MemoryStream())
                {
                    using (var jsonWriter = new Utf8JsonWriter(jsonStream))
                    {
                        jsonElement.WriteTo(jsonWriter);
                    }
                    jsonStream.Seek(0, SeekOrigin.Begin);
                    using (var reader = new StreamReader(jsonStream, leaveOpen: true))
                    {
                        requestContent = reader.ReadToEnd();
                    }
                }
            }
            else
            {
                throw new NotSupportedException($"Type ({body.GetType().FullName}) <{typeof(T).FullName}> used for SendRequest, but this Kubernetes implementation expects all calls to use JsonElement or string as the type.");
            }

            httpRequest.Content = new StringContent(requestContent, System.Text.Encoding.UTF8);
            if (method.Method == HttpMethods.Patch)
            {
                httpRequest.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/merge-patch+json; charset=utf-8");
            }
            else
            {
                httpRequest.Content.Headers.ContentType = GetHeader(body);
            }
            return SendRequestRaw(requestContent, httpRequest, cancellationToken);
        }

        protected override async Task<HttpOperationResponse<T>> CreateResultAsync<T>(HttpRequestMessage httpRequest, HttpResponseMessage httpResponse, bool? watch, CancellationToken cancellationToken)
        {
            if (typeof(T) == typeof(Eventsv1Event))
            {
                return (HttpOperationResponse<T>)(object)(await base.CreateResultAsync<Eventsv1Event>(httpRequest, httpResponse, watch, cancellationToken));
            }

            if (typeof(T) != typeof(JsonElement))
            {
                throw new NotSupportedException($"Type {typeof(T).FullName} used for CreateResultAsync, but this Kubernetes implementation expects all calls to use JsonElement as the type.");
            }

            ArgumentNullException.ThrowIfNull(httpRequest);
            ArgumentNullException.ThrowIfNull(httpResponse);

            var result = new HttpOperationResponse<T>() { Request = httpRequest, Response = httpResponse };

            if (watch == true)
            {
                throw new NotSupportedException();
            }

            try
            {
                using (Stream stream = await httpResponse.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
                {
                    result.Body = (T)(object)JsonDocument.Parse(stream).RootElement;
                }
            }
            catch (JsonException)
            {
                httpRequest.Dispose();
                httpResponse.Dispose();
                throw;
            }

            return result;
        }
    }
}
