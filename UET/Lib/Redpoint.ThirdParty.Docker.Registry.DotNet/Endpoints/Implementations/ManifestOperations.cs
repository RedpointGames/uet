namespace Docker.Registry.DotNet.Endpoints.Implementations
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text.Json.Serialization;
    using System.Threading;
    using System.Threading.Tasks;

    using Docker.Registry.DotNet.Helpers;
    using Docker.Registry.DotNet.Models;
    using Docker.Registry.DotNet.Registry;

    using JetBrains.Annotations;

    internal class ManifestOperations : IManifestOperations
    {
        private readonly NetworkClient _client;

        public ManifestOperations(NetworkClient client)
        {
            this._client = client;
        }

        public async Task<GetImageManifestResult> GetManifestAsync(
            string name,
            string reference,
            bool isDigestReference = false,
            CancellationToken cancellationToken = default)
        {
            var headers = new Dictionary<string, string>
                          {
                              {
                                  "Accept",
                                  $"{ManifestMediaTypes.ManifestSchema1}, {ManifestMediaTypes.ManifestSchema2}, {ManifestMediaTypes.ManifestList}, {ManifestMediaTypes.ManifestSchema1Signed}"
                              }
                          };

            string digestReference;
            if (!isDigestReference)
            {
                var responseLookup = await this._client.MakeRequestAsync(
                                cancellationToken,
                                HttpMethod.Head,
                                $"v2/{name}/manifests/{reference}",
                                null,
                                headers).ConfigureAwait(false);

                digestReference = responseLookup.GetHeader("docker-content-digest") ?? responseLookup.GetHeader("Docker-Content-Digest");
            }
            else
            {
                digestReference = reference;
            }

            var response = await this._client.MakeRequestAsync(
                               cancellationToken,
                               HttpMethod.Get,
                               $"v2/{name}/manifests/{digestReference}",
                               null,
                               headers).ConfigureAwait(false);

            var contentType = this.GetContentType(response.GetHeader("ContentType"), response.Body);

            switch (contentType)
            {
                case ManifestMediaTypes.ManifestSchema1:
                case ManifestMediaTypes.ManifestSchema1Signed:
                    return new GetImageManifestResult(
                               contentType,
                               this._client.JsonSerializer.DeserializeObject<ImageManifest2_1>(
                                   response.Body,
                                   DockerJsonSerializerContext.WithSettings.ImageManifest2_1),
                               response.Body)
                    {
                        DockerContentDigest = response.GetHeader("Docker-Content-Digest"),
                        Etag = response.GetHeader("Etag")
                    };

                case ManifestMediaTypes.ManifestSchema2:
                    return new GetImageManifestResult(
                               contentType,
                               this._client.JsonSerializer.DeserializeObject<ImageManifest2_2>(
                                   response.Body,
                                   DockerJsonSerializerContext.WithSettings.ImageManifest2_2),
                               response.Body)
                    {
                        DockerContentDigest = response.GetHeader("Docker-Content-Digest")
                    };

                case ManifestMediaTypes.ManifestList:
                    return new GetImageManifestResult(
                        contentType,
                        this._client.JsonSerializer.DeserializeObject<ManifestList>(response.Body,
                        DockerJsonSerializerContext.WithSettings.ManifestList),
                        response.Body);

                default:
                    throw new Exception($"Unexpected ContentType '{contentType}'.");
            }
        }

        public async Task DeleteManifestAsync(
            string name,
            string reference,
            CancellationToken cancellationToken = default)
        {
            var path = $"v2/{name}/manifests/{reference}";

            await this._client.MakeRequestAsync(cancellationToken, HttpMethod.Delete, path);
        }

        private string GetContentType(string contentTypeHeader, string manifest)
        {
            if (!string.IsNullOrWhiteSpace(contentTypeHeader))
                return contentTypeHeader;

            var check = System.Text.Json.JsonSerializer.Deserialize(
                manifest,
                DockerJsonSerializerContext.WithSettings.SchemaCheck);

            if (!string.IsNullOrWhiteSpace(check.MediaType))
                return check.MediaType;

            if (check.SchemaVersion == null)
                return ManifestMediaTypes.ManifestSchema1;

            if (check.SchemaVersion.Value == 2)
                return ManifestMediaTypes.ManifestSchema2;

            throw new Exception(
                $"Unable to determine schema type from version {check.SchemaVersion}");
        }

        [PublicAPI]
        public async Task<string> GetManifestRawAsync(
            string name,
            string reference,
            CancellationToken cancellationToken)
        {
            var headers = new Dictionary<string, string>
                          {
                              {
                                  "Accept",
                                  $"{ManifestMediaTypes.ManifestSchema1}, {ManifestMediaTypes.ManifestSchema2}, {ManifestMediaTypes.ManifestList}, {ManifestMediaTypes.ManifestSchema1Signed}"
                              }
                          };

            var response = await this._client.MakeRequestAsync(
                               cancellationToken,
                               HttpMethod.Get,
                               $"v2/{name}/manifests/{reference}",
                               null,
                               headers).ConfigureAwait(false);

            return response.Body;
        }

        public async Task<PushManifestResponse> PutManifestAsync(
            string name,
            string reference,
            ImageManifest manifest,
            CancellationToken cancellationToken)
        {
            string manifestMediaType = null;
            string jsonContent = string.Empty;
            if (manifest is ImageManifest2_1)
            {
                manifestMediaType = ManifestMediaTypes.ManifestSchema1;
                jsonContent = this._client.JsonSerializer.SerializeObject((ImageManifest2_1)manifest,
                    DockerJsonSerializerContext.WithSettings.ImageManifest2_1);
            }
            if (manifest is ImageManifest2_2)
            {
                manifestMediaType = ManifestMediaTypes.ManifestSchema2;
                jsonContent = this._client.JsonSerializer.SerializeObject((ImageManifest2_2)manifest,
                    DockerJsonSerializerContext.WithSettings.ImageManifest2_2);
            }
            if (manifest is ManifestList)
            {
                manifestMediaType = ManifestMediaTypes.ManifestList;
                jsonContent = this._client.JsonSerializer.SerializeObject((ManifestList)manifest,
                    DockerJsonSerializerContext.WithSettings.ManifestList);
            }

            var response = await this._client.MakeRequestAsync(
                                cancellationToken,
                                HttpMethod.Put,
                                $"v2/{name}/manifests/{reference}",
                                content: () =>
                                {
                                    var content = new StringContent(jsonContent);
                                    content.Headers.ContentType =
                                        new MediaTypeHeaderValue(manifestMediaType);
                                    return content;
                                }).ConfigureAwait(false);

            return new PushManifestResponse
            {
                DockerContentDigest = response.GetHeader("Docker-Content-Digest"),
                ContentLength = jsonContent.Length.ToString(),
                Location = response.GetHeader("location"),
            };
        }

        internal class SchemaCheck
        {
            /// <summary>
            ///     This field specifies the image manifest schema version as an integer.
            /// </summary>
            [JsonPropertyName("schemaVersion")]
            public int? SchemaVersion { get; set; }

            [JsonPropertyName("mediaType")]
            public string MediaType { get; set; }
        }
    }
}