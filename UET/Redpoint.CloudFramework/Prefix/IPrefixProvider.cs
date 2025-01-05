namespace Redpoint.CloudFramework.Prefix
{
    /// <summary>
    /// Maps shortened prefixes like 'u' to kinds of models like 'user'.
    /// </summary>
    public interface IPrefixProvider
    {
        /// <summary>
        /// Called by <see cref="GlobalPrefix"/> to perform prefix registration.
        /// </summary>
        /// <param name="registration">The interface that can be used by the provider to register prefixes.</param>
        void RegisterPrefixes(IPrefixRegistration registration);
    }
}
