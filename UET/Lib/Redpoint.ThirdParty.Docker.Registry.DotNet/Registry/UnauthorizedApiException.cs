namespace Docker.Registry.DotNet.Registry
{
    /// <summary>
    ///     Thrown when an api response is returned as unauthorized.
    /// </summary>
    public class UnauthorizedApiException : RegistryApiException
    {
        internal UnauthorizedApiException(RegistryApiResponse response)
            : base(response)
        {
        }
    }
}