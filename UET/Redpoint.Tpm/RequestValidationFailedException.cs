namespace Redpoint.Tpm
{
    using System;

    /// <summary>
    /// Thrown when a request was not signed with a valid client certificate, or the client certificate doesn't match the AIK PEM provided in the header.
    /// </summary>
    public class RequestValidationFailedException : Exception
    {
        public RequestValidationFailedException()
            : base("The request did not pass security validation checks.")
        {
        }
    }
}
