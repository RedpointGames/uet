namespace Redpoint.CloudFramework.Prefix
{
    using Redpoint.CloudFramework.Models;
    using System.Diagnostics.CodeAnalysis;

    public interface IPrefixRegistration
    {
        /// <summary>
        /// Register the model to use the specified prefix.
        /// </summary>
        /// <typeparam name="T">The model type.</typeparam>
        /// <param name="prefix">The prefix to use for the model.</param>
        void RegisterPrefix<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>(string prefix) where T : Model, new();
    }
}
