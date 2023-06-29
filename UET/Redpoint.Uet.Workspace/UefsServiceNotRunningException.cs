namespace Redpoint.Uet.Workspace
{
    using System;
    using System.Runtime.Serialization;

    [Serializable]
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