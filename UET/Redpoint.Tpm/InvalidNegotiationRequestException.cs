namespace Redpoint.Tpm
{
    using System;

    /// <summary>
    /// Thrown when a negotiation request is invalid during <see cref="ITpmSecuredHttpServer.HandleNegotiationRequestAsync(Microsoft.AspNetCore.Http.HttpContext)"/>.
    /// </summary>
    public class InvalidNegotiationRequestException : Exception
    {
        public InvalidNegotiationRequestException(string message)
            : base(message)
        {
        }
    }
}
