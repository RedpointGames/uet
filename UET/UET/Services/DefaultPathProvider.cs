namespace UET.Services
{
    using Redpoint.UET.Core;
    using System;
    using System.CommandLine.Invocation;
    using UET.Commands;

    internal class DefaultPathProvider : IPathProvider
    {
        private readonly string _repositoryRoot;

        public DefaultPathProvider(InvocationContext invocationContext)
        {
            var specificRepositoryRoot = invocationContext.ParseResult.GetValueForOption(GlobalOptions.RepositoryRoot);
            if (specificRepositoryRoot != null)
            {
                _repositoryRoot = specificRepositoryRoot.FullName;

                if (!Directory.Exists(BuildScripts))
                {
                    throw new InvalidOperationException("The --repository-root provided was incorrect. BuildScripts must exist inside this folder!");
                }
            }
            else
            {
                _repositoryRoot = Environment.CurrentDirectory;

                if (!Directory.Exists(BuildScripts))
                {
                    throw new InvalidOperationException("The current directory does not contain BuildScripts. Use --repository-root to set the repository root specifically.");
                }
            }
        }

        public string RepositoryRoot => _repositoryRoot;

        public string BuildScripts => Path.Combine(_repositoryRoot, "BuildScripts");

        public string BuildScriptsLib => Path.Combine(_repositoryRoot, "BuildScripts", "Lib");

        public string BuildScriptsTemp => Path.Combine(_repositoryRoot, "BuildScripts", "Temp");
    }
}
