namespace UET.Commands.Internal.CreateGitHubRelease
{
    using System;
    using System.Text.Json.Serialization.Metadata;
    using System.Text.Json;
    using System.Threading.Tasks;
    using UET.Commands.Internal.GenerateJsonSchema;
    using System.Net.Http.Headers;
    using Microsoft.Extensions.Logging;
    using System.Text;
    using System.Globalization;

    internal sealed class DefaultSchemaUploader : ISchemaUploader
    {
        private readonly ILogger<DefaultSchemaUploader> _logger;
        private readonly IJsonSchemaGenerator _jsonSchemaGenerator;

        public DefaultSchemaUploader(
            ILogger<DefaultSchemaUploader> logger,
            IJsonSchemaGenerator jsonSchemaGenerator)
        {
            _logger = logger;
            _jsonSchemaGenerator = jsonSchemaGenerator;
        }

        private static StringContent MakeContent<T>(T value, JsonTypeInfo<T> typeInfo)
        {
            return new StringContent(
                JsonSerializer.Serialize(
                    value,
                    typeInfo),
                new MediaTypeHeaderValue("application/json"));
        }

        private const string _owner = "RedpointGames";
        private const string _repo = "uet-schema";

        public async Task UpdateSchemaRepositoryAsync(
            string version,
            HttpClient client,
            CancellationToken cancellationToken)
        {
            // Release the current 'main' branch of the schema repository.
            _logger.LogInformation("Getting current 'main' branch for schema repository...");
            var response = await client.GetAsync(new Uri($"https://api.github.com/repos/{_owner}/{_repo}/branches/main"), cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var branch = JsonSerializer.Deserialize(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false), GitHubJsonSerializerContext.Default.BranchResponse);

            // Get the existing commit.
            _logger.LogInformation($"Getting commit by SHA '{branch!.Commit!.Sha}'...");
            response = await client.GetAsync(new Uri($"https://api.github.com/repos/{_owner}/{_repo}/git/commits/{branch!.Commit!.Sha}"), cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var commit = JsonSerializer.Deserialize(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false), GitHubJsonSerializerContext.Default.CommitResponse);

            // Get the existing tree, which we'll make a derived tree from.
            _logger.LogInformation($"Getting tree by SHA '{commit!.Tree!.Sha!}'...");
            response = await client.GetAsync(new Uri($"https://api.github.com/repos/{_owner}/{_repo}/git/trees/{commit!.Tree!.Sha}"), cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var tree = JsonSerializer.Deserialize(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false), GitHubJsonSerializerContext.Default.TreeResponse);

            // Generate the root schema.
            _logger.LogInformation("Generating root schema...");
            string rootSchema;
            using (var memory = new MemoryStream())
            {
                using (var writer = new Utf8JsonWriter(memory, new JsonWriterOptions { Indented = true }))
                {
                    GenerateRootSchemaJson(writer, tree!, version);
                }
                var bytes = new byte[memory.Position];
                memory.Seek(0, SeekOrigin.Begin);
                memory.Read(bytes);
                rootSchema = Encoding.UTF8.GetString(bytes);
            }

            // Generate the version schema.
            _logger.LogInformation("Generating version schema...");
            string versionSchema;
            using (var memory = new MemoryStream())
            {
                await _jsonSchemaGenerator.GenerateAsync(memory).ConfigureAwait(false);
                var bytes = new byte[memory.Position];
                memory.Seek(0, SeekOrigin.Begin);
                memory.Read(bytes);
                versionSchema = Encoding.UTF8.GetString(bytes);
            }

            // Upload both blobs.
            _logger.LogInformation("Uploading root schema blob...");
            response = await client.PostAsync(
                new Uri($"https://api.github.com/repos/{_owner}/{_repo}/git/blobs"),
                MakeContent(
                    new GitHubNewBlob
                    {
                        Content = rootSchema,
                        Encoding = "utf-8",
                    },
                    GitHubJsonSerializerContext.Default.GitHubNewBlob),
                cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var rootBlob = JsonSerializer.Deserialize(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false), GitHubJsonSerializerContext.Default.BlobPointer)!;
            _logger.LogInformation("Uploading version schema blob...");
            response = await client.PostAsync(
                new Uri($"https://api.github.com/repos/{_owner}/{_repo}/git/blobs"),
                MakeContent(
                    new GitHubNewBlob
                    {
                        Content = versionSchema,
                        Encoding = "utf-8",
                    },
                    GitHubJsonSerializerContext.Default.GitHubNewBlob),
                cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var versionBlob = JsonSerializer.Deserialize(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false), GitHubJsonSerializerContext.Default.BlobPointer)!;

            // Upload a new tree with the updated entries.
            _logger.LogInformation("Creating new tree...");
            response = await client.PostAsync(
                new Uri($"https://api.github.com/repos/{_owner}/{_repo}/git/trees"),
                MakeContent(
                    new GitHubNewTree
                    {
                        BaseTree = commit!.Tree!.Sha!,
                        Tree = new List<TreeEntry>
                        {
                            new TreeEntry
                            {
                                Mode = "100644",
                                Type = "blob",
                                Path = "root.json",
                                Sha = rootBlob.Sha,
                            },
                            new TreeEntry
                            {
                                Mode = "100644",
                                Type = "blob",
                                Path = $"{version}.json",
                                Sha = versionBlob.Sha,
                            }
                        },
                    },
                    GitHubJsonSerializerContext.Default.GitHubNewTree),
                cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var newTree = JsonSerializer.Deserialize(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false), GitHubJsonSerializerContext.Default.TreePointer)!;

            // Create the new commit.
            _logger.LogInformation("Creating new commit...");
            response = await client.PostAsync(
                new Uri($"https://api.github.com/repos/{_owner}/{_repo}/git/commits"),
                MakeContent(
                    new GitHubNewCommit
                    {
                        Message = $"Automated schema upload for {version}",
                        Tree = newTree.Sha,
                        Parents = new[] { branch!.Commit!.Sha! },
                    },
                    GitHubJsonSerializerContext.Default.GitHubNewCommit),
                cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var newCommit = JsonSerializer.Deserialize(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false), GitHubJsonSerializerContext.Default.CommitPointer)!;

            // Update the main branch.
            _logger.LogInformation("Updating the 'main' branch...");
            response = await client.PostAsync(
                new Uri($"https://api.github.com/repos/{_owner}/{_repo}/git/refs/heads/main"),
                MakeContent(
                    new GitHubUpdateRef
                    {
                        Sha = newCommit.Sha,
                        Force = false,
                    },
                    GitHubJsonSerializerContext.Default.GitHubUpdateRef),
                cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            _logger.LogInformation("The schema repository has been updated with the new schema.");
        }

        private static void GenerateRootSchemaJson(Utf8JsonWriter writer, TreeResponse tree, string newVersion)
        {
            var versions = tree.Tree!
                .Select(x => x.Path!)
                .Where(x => x.EndsWith(".json", StringComparison.Ordinal) && x != "root.json")
                .Select(x => Path.GetFileNameWithoutExtension(x))
                .Concat(new[] { newVersion })
                .Distinct()
                .OrderByDescending(x =>
                {
                    var c = x.Split(".");
                    var major = long.Parse(c[0], CultureInfo.InvariantCulture);
                    var minor = long.Parse(c[1], CultureInfo.InvariantCulture);
                    var patchStr = c[2];
                    if (patchStr.EndsWith("-pre", StringComparison.Ordinal))
                    {
                        patchStr = patchStr.Split("-")[0];
                    }
                    var patch = long.Parse(patchStr, CultureInfo.InvariantCulture);
                    return
                        (major * (366 * 60 * 24)) +
                        (minor * (60 * 24)) +
                        patch;
                })
                .ToList();

            writer.WriteStartObject();
            writer.WriteString("type", "object");
            writer.WritePropertyName("properties");
            writer.WriteStartObject();
            writer.WritePropertyName("UETVersion");
            writer.WriteStartObject();
            writer.WriteString("default", "BleedingEdge");
            writer.WritePropertyName("enum");
            writer.WriteStartArray();
            writer.WriteStringValue("BleedingEdge");
            foreach (var version in versions)
            {
                writer.WriteStringValue(version);
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
            writer.WriteEndObject();
            writer.WritePropertyName("allOf");
            writer.WriteStartArray();
            foreach (var version in versions)
            {
                if (version == newVersion)
                {
                    // Also add for BleedingEdge.
                    writer.WriteStartObject();
                    writer.WritePropertyName("if");
                    writer.WriteStartObject();
                    writer.WritePropertyName("properties");
                    writer.WriteStartObject();
                    writer.WritePropertyName("UETVersion");
                    writer.WriteStartObject();
                    writer.WriteString("const", "BleedingEdge");
                    writer.WriteEndObject();
                    writer.WriteEndObject();
                    writer.WriteEndObject();
                    writer.WritePropertyName("then");
                    writer.WriteStartObject();
                    writer.WriteString("$ref", $"https://raw.githubusercontent.com/RedpointGames/uet-schema/main/{version}.json");
                    writer.WriteEndObject();
                    writer.WriteEndObject();
                }

                writer.WriteStartObject();
                writer.WritePropertyName("if");
                writer.WriteStartObject();
                writer.WritePropertyName("properties");
                writer.WriteStartObject();
                writer.WritePropertyName("UETVersion");
                writer.WriteStartObject();
                writer.WriteString("const", version);
                writer.WriteEndObject();
                writer.WriteEndObject();
                writer.WriteEndObject();
                writer.WritePropertyName("then");
                writer.WriteStartObject();
                writer.WriteString("$ref", $"https://raw.githubusercontent.com/RedpointGames/uet-schema/main/{version}.json");
                writer.WriteEndObject();
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
    }
}
