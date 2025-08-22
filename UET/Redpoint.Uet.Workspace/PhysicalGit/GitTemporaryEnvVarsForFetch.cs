namespace Redpoint.Uet.Workspace.PhysicalGit
{
    using System;
    using System.Collections.Generic;

    internal class GitTemporaryEnvVarsForFetch : IDisposable
    {
        private readonly string? _path;
        private readonly IReadOnlyDictionary<string, string>? _envVars;

        public GitTemporaryEnvVarsForFetch(IReadOnlyDictionary<string, string> envVars)
        {
            _path = null;
            _envVars = envVars;
        }

        public GitTemporaryEnvVarsForFetch(
            IReadOnlyDictionary<string, string> envVars,
            string privateKey)
        {
            _path = Path.GetTempFileName();
            using (var stream = new StreamWriter(new FileStream(_path, FileMode.Create, FileAccess.ReadWrite, FileShare.None)))
            {
                // @note: Private key content *must* have a newline at the end.
                stream.Write(privateKey.Replace("\r\n", "\n", StringComparison.Ordinal).Trim() + "\n");
            }

            // @note: The identity file path format is extremely jank.
            var identityPath = _path;
            if (OperatingSystem.IsWindows())
            {
                var root = Path.GetPathRoot(identityPath)!;
                root = $"/{root[0].ToString().ToLowerInvariant()}";
                identityPath = identityPath[root.Length..];
                identityPath = root + "/" + identityPath.Replace("\\", "/", StringComparison.Ordinal).TrimStart('/');
            }
            identityPath = identityPath.Replace(" ", "\\ ", StringComparison.Ordinal);

            var newEnvVars = new Dictionary<string, string>(envVars);
            newEnvVars["GIT_SSH_COMMAND"] = $@"ssh -o IdentitiesOnly=yes -o StrictHostKeyChecking=accept-new -i {identityPath}";
            _envVars = newEnvVars;
        }

        public IReadOnlyDictionary<string, string>? EnvironmentVariables => _envVars;

        public void Dispose()
        {
            if (_path != null)
            {
                File.Delete(_path);
            }
        }
    }
}
