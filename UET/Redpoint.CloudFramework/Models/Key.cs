namespace Redpoint.CloudFramework.Models
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Text;
    using DatastoreKey = Google.Cloud.Datastore.V1.Key;

    public sealed class Key<[DynamicallyAccessedMembers(DynamicReferencePolicy.ModelPolicy)] T>
        : UntypedKey
        , IEquatable<Key<T>?>
        where T : class, IModel, new()
    {
        internal Key(DatastoreKey datastoreKey)
            : base(datastoreKey)
        {
            if (datastoreKey.Path.Last().Kind != ReferenceModelCache.Get<T>().Kind)
            {
                throw new ArgumentException($"The provided key has kind '{datastoreKey.Path.Last().Kind}' as the last path element, but expected '{ReferenceModelCache.Get<T>().Kind}' for this model type.", nameof(datastoreKey));
            }
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as Key<T>);
        }

        public bool Equals(Key<T>? other)
        {
            return Equals(other as UntypedKey);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public static bool operator ==(Key<T>? left, Key<T>? right)
        {
            return EqualityComparer<Key<T>>.Default.Equals(left, right);
        }

        public static bool operator !=(Key<T>? left, Key<T>? right)
        {
            return !(left == right);
        }
    }
}
