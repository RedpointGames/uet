namespace Redpoint.CloudFramework.Repository
{
    using Google.Cloud.Datastore.V1;
    using System.Linq;

    public static class KeyExtensions
    {
        public static long GetIdFromKey(this Key key)
        {
            ArgumentNullException.ThrowIfNull(key);

            return key.Path.Last().Id;
        }

        public static string GetNameFromKey(this Key key)
        {
            ArgumentNullException.ThrowIfNull(key);

            return key.Path.Last().Name;
        }
    }
}
