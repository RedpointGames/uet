namespace Redpoint.CloudFramework.Repository.Hooks
{
    using Google.Cloud.Datastore.V1;
    using Redpoint.CloudFramework.Models;
    using Redpoint.CloudFramework.Repository.Transaction;
    using System.Threading.Tasks;

    public interface IGlobalRepositoryHook
    {
        Task PostCreate<T>(string @namespace, T model, IModelTransaction? transaction) where T : class, IModel, new();
        Task PostUpsert<T>(string @namespace, T model, IModelTransaction? transaction) where T : class, IModel, new();
        Task PostUpdate<T>(string @namespace, T model, IModelTransaction? transaction) where T : class, IModel, new();
        Task PostDelete<T>(string @namespace, T model, IModelTransaction? transaction) where T : class, IModel, new();

        Task MutateEntityBeforeWrite(string @namespace, Entity entity);
    }
}
