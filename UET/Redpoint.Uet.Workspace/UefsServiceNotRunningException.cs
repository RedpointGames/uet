namespace Redpoint.Uet.Workspace
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Runtime.Serialization;

    [Serializable]
    [SuppressMessage("Design", "CA1064:Exceptions should be public", Justification = "This exception is only used internally.")]
    internal class UefsServiceNotRunningException : Exception
    {
        public UefsServiceNotRunningException() : base("The UEFS service is not running on this machine, so the requested workspace can not be provided.")
        {
        }

        public UefsServiceNotRunningException(Exception? innerException) : base("The UEFS service is not running on this machine, so the requested workspace can not be provided.", innerException)
        {
        }

        protected UefsServiceNotRunningException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}