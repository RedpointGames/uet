namespace Redpoint.CloudFramework.Repository.Hooks
{
    using Google.Cloud.Datastore.V1;
    using Redpoint.CloudFramework.Models;
    using Redpoint.CloudFramework.Repository.Transaction;
    using System.Threading.Tasks;

    public interface IGlobalRepositoryHook
    {
        Task PostCreate<T>(string @namespace, T model, IModelTransaction? transaction) where T : Model, new();
        Task PostUpsert<T>(string @namespace, T model, IModelTransaction? transaction) where T : Model, new();
        Task PostUpdate<T>(string @namespace, T model, IModelTransaction? transaction) where T : Model, new();
        Task PostDelete<T>(string @namespace, T model, IModelTransaction? transaction) where T : Model, new();

        Task MutateEntityBeforeWrite(string @namespace, Entity entity);
    }
}
