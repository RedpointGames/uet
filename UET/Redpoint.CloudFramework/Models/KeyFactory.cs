namespace Redpoint.CloudFramework.Models
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Text;
    using DatastoreKeyFactory = Google.Cloud.Datastore.V1.KeyFactory;

    public sealed class KeyFactory<[DynamicallyAccessedMembers(DynamicReferencePolicy.ModelPolicy)] T> where T : class, IModel, new()
    {
        private readonly DatastoreKeyFactory _datastoreKeyFactory;

        internal KeyFactory(DatastoreKeyFactory datastoreKeyFactory)
        {
            _datastoreKeyFactory = datastoreKeyFactory;
        }

        public Key<T> CreateKey(long id)
        {
            return new Key<T>(_datastoreKeyFactory.CreateKey(id));
        }

        public Key<T> CreateKey(string name)
        {
            return new Key<T>(_datastoreKeyFactory.CreateKey(name));
        }

        public Key<T> CreateIncompleteKey()
        {
            return new Key<T>(_datastoreKeyFactory.CreateIncompleteKey());
        }
    }
}
