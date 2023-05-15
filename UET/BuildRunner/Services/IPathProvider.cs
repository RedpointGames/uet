namespace BuildRunner.Services
{
    internal interface IPathProvider
    {
        string RepositoryRoot { get; }

        string BuildScripts { get; }

        string BuildScriptsLib { get; }

        string BuildScriptsTemp { get; }
    }
}
