namespace Redpoint.CloudFramework.Repository.Converters.Model
{
    using Google.Cloud.Datastore.V1;
    using Redpoint.CloudFramework.Models;
    using System;

    internal interface IModelConverter<TOther>
    {
        T? From<T>(string @namespace, TOther data) where T : class, IModel, new();

        TOther To<T>(string @namespace, T? model, bool isCreateContext, Func<T, Key>? incompleteKeyFactory) where T : class, IModel, new();
    }
}
