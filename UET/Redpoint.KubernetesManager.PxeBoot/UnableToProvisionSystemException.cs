namespace Redpoint.KubernetesManager.PxeBoot
{
    using System;

    public class UnableToProvisionSystemException : Exception
    {
        public UnableToProvisionSystemException(string message)
            : base(message)
        {
        }
    }
}
