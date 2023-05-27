namespace Redpoint.Git.GitHub
{
    using Octokit;
    using Redpoint.Git.Abstractions;
    using System;
    using System.IO;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;

    public class GitHubGitRepository : IGitRepository
    {
        private readonly GitHubClient _client;
        private readonly string _owner;
        private readonly string _repo;

        public GitHubGitRepository(
            GitHubClient client,
            string owner,
            string repo)
        {
            _client = client;
            _owner = owner;
            _repo = repo;
        }

        public void Dispose()
        {
        }

        public async Task<string?> ResolveRefToShaAsync(string @ref, CancellationToken cancellationToken)
        {
            if (Regex.IsMatch(@ref, "^[0-9a-f]{40}$"))
            {
                return @ref;
            }
            var resolvedRef = await _client.Git.Reference.Get(_owner, _repo, @ref);
            return resolvedRef.Object.Sha;
        }

        public async Task<IGitCommit> GetCommitByShaAsync(string sha, CancellationToken cancellationToken)
        {
            var commit = await _client.Git.Commit.Get(_owner, _repo, sha);
            return new GitHubGitCommit(_client, _owner, _repo, commit);
        }

        public async Task<long> MaterializeBlobToDiskByShaAsync(string sha, string destinationPath, Func<string, string>? contentAdjust, CancellationToken cancellationToken)
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("User-Agent", $"uefs.redpoint.games");
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_client.Credentials.GetToken()}");
                client.DefaultRequestHeaders.Add("Accept", "application/vnd.github.raw");
                client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");

                var url = $"https://api.github.com/repos/{_owner}/{_repo}/git/blobs/{sha}";
                var response = await client.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    var message = await response.Content.ReadAsStringAsync();
                    response.EnsureSuccessStatusCode();
                }

                using (var stream = await response.Content.ReadAsStreamAsync())
                {
                    if (contentAdjust != null)
                    {
                        using (var memory = new MemoryStream())
                        {
                            await stream.CopyToAsync(memory);
                            var b = new byte[memory.Position];
                            memory.Seek(0, SeekOrigin.Begin);
                            memory.Read(b);

                            using (var writer = new StreamWriter(new FileStream(destinationPath, System.IO.FileMode.Create, FileAccess.Write, FileShare.None)))
                            {
                                await writer.WriteAsync(contentAdjust(Encoding.UTF8.GetString(b)));
                            }
                        }
                    }
                    else
                    {
                        using (var writer = new FileStream(destinationPath, System.IO.FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            await stream.CopyToAsync(writer);
                        }
                    }
                    return stream.Length;
                }
            }
        }
    }
}
