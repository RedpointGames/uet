namespace Redpoint.CloudFramework.Prefix
{
    using System.Threading.Tasks;
    using Google.Cloud.Datastore.V1;
    using Redpoint.CloudFramework.Models;

    public interface IPrefix
    {
        string Create(Key key);
        string CreateInternal(Key key);
        Task<Key> Parse(string identifier);
        Task<Key> ParseInternal(string identifier);
        Task<Key> ParseLimited(string identifier, string kind);
        Task<Key> ParseLimited<T>(string identifier) where T : class, IModel, new();
    }
}
