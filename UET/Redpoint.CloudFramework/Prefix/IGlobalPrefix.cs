namespace Redpoint.CloudFramework.Prefix
{
    using Redpoint.CloudFramework.Models;
    using System.Diagnostics.CodeAnalysis;

    public interface IGlobalPrefix
    {
        string Create(UntypedKey key);
        string CreateInternal(UntypedKey key, PathGenerationMode pathGenerationMode = PathGenerationMode.Default);
        UntypedKey Parse(string datastoreNamespace, string identifier);
        UntypedKey ParseInternal(string datastoreNamespace, string identifier);
        UntypedKey ParseLimited(string datastoreNamespace, string identifier, string kind);
        Key<T> ParseLimited<[DynamicallyAccessedMembers(DynamicReferencePolicy.ModelPolicy)] T>(string datastoreNamespace, string identifier) where T : class, IModel, new();
        bool IsType<T>(string identifier) where T : class, IModel, new();
        bool TryParseLimited<[DynamicallyAccessedMembers(DynamicReferencePolicy.ModelPolicy)] T>(string datastoreNamespace, string identifier, out Key<T> validatedKey) where T : class, IModel, new();
    }
}
