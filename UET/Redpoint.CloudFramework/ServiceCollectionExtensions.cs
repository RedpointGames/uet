namespace Redpoint.CloudFramework
{
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.CloudFramework.Prefix;
    using System.Diagnostics.CodeAnalysis;

    /// <summary>
    /// Provides additional service registration methods for <see cref="IServiceCollection"/>.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds the specified prefix provider to the service collection.
        /// </summary>
        /// <typeparam name="T">The prefix provider implementation.</typeparam>
        /// <param name="services">The service collection to register it with.</param>
        public static void AddPrefixProvider<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>(this IServiceCollection services) where T : class, IPrefixProvider
        {
            services.AddSingleton<IPrefixProvider, T>();
        }
    }
}
