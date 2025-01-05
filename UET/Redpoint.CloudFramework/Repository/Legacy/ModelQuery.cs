namespace Redpoint.CloudFramework.Datastore
{
    using Google.Cloud.Datastore.V1;
    using Redpoint.CloudFramework.Models;

    public class ModelQuery<T> where T : Model, new()
    {
        public ModelQuery(string @namespace, Query query)
        {
            Namespace = @namespace;
            Query = query;
        }

        public string Namespace { get; }
        public Query Query { get; }
    }
}
