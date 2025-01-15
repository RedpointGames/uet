namespace Redpoint.CloudFramework.Prefix
{
    using Google.Cloud.Datastore.V1;
    using Redpoint.CloudFramework.Models;

    public interface IGlobalPrefix
    {
        string Create(Key key);
        string CreateInternal(Key key, PathGenerationMode pathGenerationMode = PathGenerationMode.Default);
        Key Parse(string datastoreNamespace, string identifier);
        Key ParseInternal(string datastoreNamespace, string identifier);
        Key ParseLimited(string datastoreNamespace, string identifier, string kind);
        Key ParseLimited<T>(string datastoreNamespace, string identifier) where T : class, IModel, new();
    }
}
