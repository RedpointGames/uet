namespace Redpoint.UET.Core
{
    public interface IPathProvider
    {
        string RepositoryRoot { get; }

        string BuildScripts { get; }

        string BuildScriptsLib { get; }

        string BuildScriptsTemp { get; }
    }
}
