namespace Redpoint.Uet.Workspace
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Runtime.Serialization;

    [Serializable]
    [SuppressMessage("Design", "CA1064:Exceptions should be public", Justification = "This exception is only used internally.")]
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
    }
}