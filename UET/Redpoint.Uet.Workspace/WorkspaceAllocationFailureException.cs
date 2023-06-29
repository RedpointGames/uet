namespace Redpoint.Uet.Workspace
{
    using System;
    using System.Runtime.Serialization;

    [Serializable]
    internal class WorkspaceAllocationFailureException : Exception
    {
        public WorkspaceAllocationFailureException()
        {
        }

        public WorkspaceAllocationFailureException(string? message) : base(message)
        {
        }

        public WorkspaceAllocationFailureException(string? message, Exception? innerException) : base(message, innerException)
        {
        }

        protected WorkspaceAllocationFailureException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}