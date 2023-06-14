namespace Docker.Registry.DotNet.Endpoints.Implementations
{
    using Docker.Registry.DotNet.Helpers;
    using Docker.Registry.DotNet.Models;
    using Docker.Registry.DotNet.Registry;
    using System;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Threading;
    using System.Threading.Tasks;

    internal class BlobUploadOperations : IBlobUploadOperations
    {
        private readonly NetworkClient _client;

        internal BlobUploadOperations(NetworkClient client)
        {
            this._client = client;
        }

        public async Task<ResumableUpload> StartUploadBlobAsync(string name, CancellationToken cancellationToken = default)
        {
            var path = $"v2/{name}/blobs/uploads/";
            var response = await this._client.MakeRequestAsync(
                               cancellationToken,
                               HttpMethod.Post,
                               path);
            return new ResumableUpload()
            {
                DockerUploadUuid = response.Headers.GetString("Docker-Upload-UUID"),
                Location = response.Headers.GetString("location"),
                Range = response.Headers.GetString("Range")
            };
        }


        public Task<CompletedUploadResponse> MonolithicUploadBlobAsync(ResumableUpload resumable,
                                                                       string digest,
                                                                       Stream stream,
                                                                       CancellationToken cancellationToken = default)
        {
            return CompleteBlobUploadAsync(resumable, digest, stream, cancellationToken: cancellationToken);
        }

        /// <summary>
        ///     Perform a monolithic upload.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="contentLength"></param>
        /// <param name="stream"></param>
        /// <param name="digest"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task UploadBlobAsync(
            string name,
            int contentLength,
            Stream stream,
            string digest,
            CancellationToken cancellationToken = default)
        {
            var path = $"v2/{name}/blobs/uploads/";

            var response = await this._client.MakeRequestAsync(
                               cancellationToken,
                               HttpMethod.Post,
                               path);

            var uuid = response.Headers.GetString("Docker-Upload-UUID");

            Console.WriteLine($"Uploading with uuid: {uuid}");

            var location = response.Headers.GetString("Location");

            Console.WriteLine($"Using location: {location}");

            //await GetBlobUploadStatus(name, uuid, cancellationToken);

            try
            {
                using (var client = new HttpClient())
                {
                    var progressResponse = await client.GetAsync(location, cancellationToken);

                    //Send the contents of the whole file
                    var content = new StreamContent(stream);

                    content.Headers.ContentLength = stream.Length;
                    content.Headers.ContentType =
                        new MediaTypeHeaderValue("application/octet-stream");
                    content.Headers.ContentRange = new ContentRangeHeaderValue(0, stream.Length);

                    var request = new HttpRequestMessage(
                                      new HttpMethod("PATCH"),
                                      location + $"&digest={digest}")
                    {
                        Content = content
                    };

                    var response2 = await client.SendAsync(request, cancellationToken);

                    if (response2.StatusCode < HttpStatusCode.OK
                        || response2.StatusCode >= HttpStatusCode.BadRequest)
                        throw new RegistryApiException(
                            new RegistryApiResponse<string>(
                                response2.StatusCode,
                                null,
                                response.Headers));

                    progressResponse = await client.GetAsync(location, cancellationToken);
                }

                ////{

                ////    var queryString = new QueryString();

                ////    queryString.Add("digest", digest);

                ////    var content = new StreamContent(stream);

                ////    content.Headers.ContentLength = 0;
                ////    content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                ////    //content.Headers.ContentRange = new ContentRangeHeaderValue(0, stream.Length);

                ////    await _client.MakeRequestAsync(cancellationToken, HttpMethod.Put, $"v2/{name}/blobs/uploads/{uuid}",
                ////        queryString);
                ////}

                //using (var client = new HttpClient())
                //{
                //    var content = new StringContent("");

                //    content.Headers.ContentLength = 0;
                //    //content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

                //    var request = new HttpRequestMessage(HttpMethod.Put, new Uri($"http://10.0.4.44:5000/v2/{name}/blobs/uploads/{uuid}&digest={digest}"))
                //    {
                //        Content = content
                //    };

                //    var response2 = await client.SendAsync(request, cancellationToken);

                //    //content.Headers.ContentRange = new ContentRangeHeaderValue(0, stream.Length);

                //    if (response2.StatusCode < HttpStatusCode.OK || response2.StatusCode >= HttpStatusCode.BadRequest)
                //    {
                //        throw new RegistryApiException(new RegistryApiResponse<string>(response2.StatusCode, null, response.Headers));
                //    }
                //}
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine("Attempting to cancel the upload...");

                await this._client.MakeRequestAsync(
                    cancellationToken,
                    HttpMethod.Delete,
                    $"v2/{name}/blobs/uploads/{uuid}");

                throw;
            }

            //string path2 = $"v2/{name}/blobs/uploads/{uuid}";

            //var response2 = await _client.MakeRequestAsync(cancellationToken, HttpMethod.Put, path2, queryString);

            //await _client.MakeRequestAsync(cancellationToken, HttpMethod.Put, location, queryString);
        }

        public Task<ResumableUpload> InitiateBlobUploadAsync(
            string name,
            Stream stream = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public async Task<MountResponse> MountBlobAsync(
            string name,
            MountParameters parameters,
            CancellationToken cancellationToken = default)
        {
            var queryString = new QueryString();
            queryString.Add("mount", parameters.Digest);
            queryString.Add("from", parameters.From);

            var response = await this._client.MakeRequestAsync(cancellationToken,
                     HttpMethod.Post,
                     $"v2/{name}/blobs/uploads/",
                     queryString);
            return new MountResponse
            {
                DockerUploadUuid = response.Headers.GetString("Docker-Upload-UUID"),
                Location = response.Headers.GetString("location"),
                Created = response.StatusCode == HttpStatusCode.Created,
            };
        }

        public async Task<BlobUploadStatus> GetBlobUploadStatus(
            string name,
            string uuid,
            CancellationToken cancellationToken = default)
        {
            var response = await this._client.MakeRequestAsync(cancellationToken,
                    HttpMethod.Get,
                    $"v2/{name}/blobs/uploads/{uuid}");

            return new BlobUploadStatus
            {
                DockerUploadUuid = response.Headers.GetString("Docker-Upload-UUID"),
                Range = response.Headers.GetString("Range")
            };
        }

        public async Task<ResumableUpload> UploadBlobChunkAsync(ResumableUpload resumable,
                                                          Stream chunk,
                                                          long? from = null,
                                                          long? to = null,
                                                          CancellationToken cancellationToken = default)
        {
            var response = await this._client.MakeRequestAsync(
                              cancellationToken,
                              new HttpMethod("PATCH"),
                              resumable.Location,
                              content: () =>
                              {
                                  chunk.Position = 0;
                                  var content = new StreamContent(chunk);
                                  content.Headers.ContentLength = chunk.Length;
                                  content.Headers.ContentType =
                                      new MediaTypeHeaderValue("application/octet-stream");
                                  content.Headers.ContentRange = new ContentRangeHeaderValue(from ?? 0, to ?? chunk.Length);
                                  return content;
                              }).ConfigureAwait(false);

            return new ResumableUpload()
            {
                DockerUploadUuid = response.Headers.GetString("Docker-Upload-UUID"),
                Location = response.Headers.GetString("location"),
                Range = response.Headers.GetString("Range")
            };
        }

        public async Task<CompletedUploadResponse> CompleteBlobUploadAsync(ResumableUpload resumable,
                                                     string digest,
                                                     Stream chunk = null,
                                                     long? from = null,
                                                     long? to = null,
                                                     CancellationToken cancellationToken = default)
        {
            var queryString = new QueryString();
            queryString.Add("digest", digest);

            var response = await this._client.MakeRequestAsync(
                              cancellationToken,
                              HttpMethod.Put,
                              resumable.Location,
                              queryString,
                              content: () =>
                                {
                                    if (chunk is null) chunk = new MemoryStream();
                                    chunk.Position = 0;
                                    var content = new StreamContent(chunk);
                                    content.Headers.ContentLength = chunk.Length;
                                    content.Headers.ContentType =
                                        new MediaTypeHeaderValue("application/octet-stream");
                                    content.Headers.ContentRange = new ContentRangeHeaderValue(from ?? 0, to ?? chunk.Length);
                                    return content;
                                }).ConfigureAwait(false);

            return new CompletedUploadResponse()
            {
                DockerContentDigest = response.Headers.GetString("Docker-Content-Digest"),
                Location = response.Headers.GetString("location"),
            };
        }

        public Task CancelBlobUploadAsync(
            string name,
            string uuid,
            CancellationToken cancellationToken = default)
        {
            var path = $"v2/{name}/blobs/uploads/{uuid}";

            return this._client.MakeRequestAsync(cancellationToken, HttpMethod.Delete, path);
        }

    }
}