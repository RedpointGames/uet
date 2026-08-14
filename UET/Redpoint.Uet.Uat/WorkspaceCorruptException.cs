namespace Redpoint.Uet.Uat
{
    public class WorkspaceCorruptException : Exception
    {
        public WorkspaceCorruptException()
            : base("The workspace is corrupt and must be cleared for a fresh rebuild.")
        {
        }
    }
}