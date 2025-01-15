namespace Redpoint.CloudFramework.Event
{
    using Google.Cloud.Datastore.V1;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Http;

    public interface IEventApi
    {
#pragma warning disable CA1030 // Use events where appropriate
        Task Raise(string eventType, Key project, Key session, HttpRequest request, Key key, object entity, object userdata);
#pragma warning restore CA1030 // Use events where appropriate
    }
}
