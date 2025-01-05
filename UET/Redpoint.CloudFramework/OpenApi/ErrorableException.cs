namespace Redpoint.CloudFramework.OpenApi
{
    using System;

    public class ErrorableException : Exception
    {
        public ErrorableException(string? message) : base(message)
        {
        }

        public required int StatusCode { get; init; }
    }
}
