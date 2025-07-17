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

        public static bool IsOneOfString(this string? value, string[] target)
        {
            return value != null && target.Contains(value);
        }

        public static bool IsNotOneOfString(this string? value, string[] target)
        {
            return !(value != null && target.Contains(value));
        }
    }
}
