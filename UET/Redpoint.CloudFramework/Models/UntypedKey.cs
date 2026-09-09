namespace Redpoint.CloudFramework.Models
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using static Google.Cloud.Datastore.V1.Key.Types;
    using DatastoreKey = Google.Cloud.Datastore.V1.Key;

    public class UntypedKey
        : IEquatable<UntypedKey?>
    {
        private readonly DatastoreKey _key;

        internal UntypedKey(DatastoreKey datastoreKey)
        {
            ArgumentNullException.ThrowIfNull(datastoreKey);
            _key = datastoreKey;
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as UntypedKey);
        }

        public bool Equals(UntypedKey? other)
        {
            return other is not null &&
                   EqualityComparer<DatastoreKey>.Default.Equals(_key, other._key);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(_key);
        }

        public static bool operator ==(UntypedKey? left, UntypedKey? right)
        {
            return EqualityComparer<UntypedKey>.Default.Equals(left, right);
        }

        public static bool operator !=(UntypedKey? left, UntypedKey? right)
        {
            return !(left == right);
        }

        internal DatastoreKey __InternalDatastoreKey__ => _key;

        [Obsolete("Use prefixed IDs instead of using GetIdFromKey")]
        public long GetIdFromKey()
        {
            return _key.Path.Last().Id;
        }

        public string GetNameFromKey()
        {
            return _key.Path.Last().Name;
        }

        public Key<T> WithIncompleteElement<[DynamicallyAccessedMembers(DynamicReferencePolicy.ModelPolicy)] T>() where T : class, IModel, new()
        {
            return new Key<T>(_key.WithElement(new PathElement { Kind = ReferenceModelCache.Get<T>().Kind }));
        }

        public Key<T> WithElement<[DynamicallyAccessedMembers(DynamicReferencePolicy.ModelPolicy)] T>(long id) where T : class, IModel, new()
        {
            return new Key<T>(_key.WithElement(ReferenceModelCache.Get<T>().Kind, id));
        }

        public Key<T> WithElement<[DynamicallyAccessedMembers(DynamicReferencePolicy.ModelPolicy)] T>(string name) where T : class, IModel, new()
        {
            return new Key<T>(_key.WithElement(ReferenceModelCache.Get<T>().Kind, name));
        }
    }
}
