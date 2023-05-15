namespace UET.Commands
{
    using System.CommandLine;

    internal static class GlobalOptions
    {
        public static Option<DirectoryInfo> RepositoryRoot = new Option<DirectoryInfo>(
            name: "--repository-root",
            description: "The path to the repository root (BuildScripts should be directly located in this folder).");
    }
}
