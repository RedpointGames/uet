namespace Redpoint.CloudFramework.Repository
{
    using Google.Cloud.Datastore.V1;

    public static class RepositoryExtensions
    {
        public static bool HasAncestor(this Key key, Key? parent)
        {
            ArgumentNullException.ThrowIfNull(key);

            var keyParent = key.GetParent();
            if (keyParent != null)
            {
                return keyParent.Equals(parent);
            }
            return false;
        }

        public static bool IsAnyString(this string[]? values, string target)
        {
            return values != null && values.Contains(target);
        }

        /// <summary>
        /// Match if the property is one of the values specified by <paramref name="target"/>. A maximum of 30 values is permitted by Firestore; any more than this and you will receive an error.
        /// </summary>
        /// <param name="value">The property to query against.</param>
        /// <param name="target">The target values that must match in order for an entity to be returned.</param>
        /// <returns>True if the property value is in the target array.</returns>
        public static bool IsOneOfString(this string? value, string[] target)
        {
            ArgumentNullException.ThrowIfNull(target);

            // This check is also present in DefaultExpressionConverter, which is what enforces it client side for actual queries.
            if (target.Length > 30)
            {
                throw new InvalidOperationException("The target array for an 'IsOneOfString' has more than 30 entries to match against. Firestore only permits up to 30 entries for an 'IN' query.");
            }

            return value != null && target.Contains(value);
        }

        /// <summary>
        /// Match if the property is one of the values specified by <paramref name="target"/>. A maximum of 10 values is permitted by Firestore; any more than this and you will receive an error.
        /// </summary>
        /// <param name="value">The property to query against.</param>
        /// <param name="target">The target values that must not match in order for an entity to be returned.</param>
        /// <returns>True if the property value is not in the target array.</returns>
        public static bool IsNotOneOfString(this string? value, string[] target)
        {
            ArgumentNullException.ThrowIfNull(target);

            // This check is also present in DefaultExpressionConverter, which is what enforces it client side for actual queries.
            if (target.Length > 30)
            {
                throw new InvalidOperationException("The target array for an 'IsNotOneOfString' has more than 10 entries to match against. Firestore only permits up to 10 entries for a 'NOT-IN' query.");
            }

            return !(value != null && target.Contains(value));
        }
    }
}
