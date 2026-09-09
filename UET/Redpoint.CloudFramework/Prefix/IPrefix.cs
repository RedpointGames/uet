namespace Redpoint.CloudFramework.Prefix
{
    using Redpoint.CloudFramework.Models;
    using System.Diagnostics.CodeAnalysis;
    using System.Threading.Tasks;

    public interface IPrefix
    {
        string Create(UntypedKey key);
        string CreateInternal(UntypedKey key);
        Task<UntypedKey> Parse(string identifier);
        Task<UntypedKey> ParseInternal(string identifier);
        Task<UntypedKey> ParseLimited(string identifier, string kind);
        Task<Key<T>> ParseLimited<[DynamicallyAccessedMembers(DynamicReferencePolicy.ModelPolicy)] T>(string identifier) where T : class, IModel, new();
    }
}
