namespace Redpoint.CloudFramework.Repository.Converters.Value.Context
{
    using Redpoint.CloudFramework.Models;
    using Type = Type;

    /// <summary>
    /// A delegate which is called by the conversion after all other fields are loaded, with the 
    /// local namespace value provided by <see cref="Model.GetDatastoreNamespaceForLocalKeys"/>.
    /// </summary>
    /// <param name="localNamespace">The local namespace value provided by <see cref="Model.GetDatastoreNamespaceForLocalKeys"/>.</param>
    /// <returns>The CLR value that would normally have been directly returned from <see cref="IValueConverter.ConvertFromDatastoreValue(DatastoreValueConvertFromContext, string, Type, Google.Cloud.Datastore.V1.Value, AddConvertFromDelayedLoad)"/> or <see cref="IValueConverter.ConvertFromJsonToken(JsonValueConvertFromContext, string, Type, Newtonsoft.Json.Linq.JToken, AddConvertFromDelayedLoad)"/>.</returns>
    internal delegate object? ConvertFromDelayedLoad(string localNamespace);
}
