using System.Text.RegularExpressions;

namespace Redpoint.Uefs.ContainerRegistry
{
    /// <summary>
    /// Provides a regular expression for parsing container images (like <c>host.com/path/subpath/image:tag</c>).
    /// </summary>
    public static class RegistryTagRegex
    {
        /// <summary>
        /// The regular expression used for parsing container images (like <c>host.com/path/subpath/image:tag</c>).
        /// </summary>
        public static readonly Regex Regex = new Regex("^(?<host>[a-z\\.\\:0-9]+)/(?<path>[a-z\\./0-9-_]+):?(?<label>[a-z\\.0-9-_]+)?$");
    }
}
