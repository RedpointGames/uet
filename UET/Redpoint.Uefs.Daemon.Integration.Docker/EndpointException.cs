namespace Redpoint.Uefs.Daemon.Integration.Docker
{
    public class EndpointException<TResponse> : Exception where TResponse : notnull
    {
        public EndpointException(int statusCode, TResponse errorResponse)
        {
            StatusCode = statusCode;
            ErrorResponse = errorResponse;
        }

        public int StatusCode { get; }
        public TResponse ErrorResponse { get; }
    }
}
