namespace Redpoint.UET.Workspace
{
    using System.Threading.Tasks;

    internal class LocalWorkspace : IWorkspace
    {
        public LocalWorkspace(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
