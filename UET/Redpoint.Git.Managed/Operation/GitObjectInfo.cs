namespace Redpoint.Git.Managed.Operation
{
    using Redpoint.Git.Managed.Packfile;

    internal class GitObjectInfo : IDisposable
    {
        public required GitObjectType Type { get; set; }
        public required ulong Size { get; set; }
        public required Stream? Data { get; set; }

        public void Dispose()
        {
            if (Data != null)
            {
                Data.Dispose();
            }
        }
    }
}