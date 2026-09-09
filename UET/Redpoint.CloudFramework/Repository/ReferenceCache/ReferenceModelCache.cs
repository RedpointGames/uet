namespace Redpoint.CloudFramework.Repository.ReferenceCache
{
    using Redpoint.CloudFramework.Models;
    using System;
    using System.Collections.Generic;

    public static class ReferenceModelCache
    {
        private static Dictionary<Type, IReferenceModel> _referenceCache = new();

        /// <summary>
        /// Returns a reference model representing static metadata about a model type.
        /// </summary>
        public static IReferenceModel<T> Get<T>() where T : class, IModel, new()
        {
            var type = typeof(T);
            if (!_referenceCache.TryGetValue(type, out var result))
            {
                result = new ReferenceModel<T>(new T());
                _referenceCache.Add(type, result);
            }
            return (IReferenceModel<T>)result;
        }
    }
}
