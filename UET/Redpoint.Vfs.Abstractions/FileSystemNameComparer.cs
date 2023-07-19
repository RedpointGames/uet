namespace Redpoint.Vfs.Abstractions
{
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;

    /// <summary>
    /// Compares string values so that they are ordered correctly for a virtual filesystem driver.
    /// </summary>
    public class FileSystemNameComparer : IComparer<string>, IEqualityComparer<string>
    {
        private CompareInfo _compareInfo = CultureInfo.InvariantCulture.CompareInfo;

        /// <summary>
        /// Compares two filenames such that they are ordered correctly for a virtual filesystem driver.
        /// </summary>
        /// <param name="a">The first filename.</param>
        /// <param name="b">The second filename.</param>
        /// <returns>The relative order.</returns>
        public int Compare(string? a, string? b)
        {
            string? sa = a as string;
            string? sb = b as string;
            if (sa != null && sb != null)
            {
                if (sa == ".")
                {
                    if (sb == ".")
                    {
                        return 0;
                    }

                    return -1;
                }
                else if (sa == "..")
                {
                    if (sb == ".")
                    {
                        return 1;
                    }
                    else if (sb == "..")
                    {
                        return 0;
                    }

                    return -1;
                }
                else if (sb == "." || sb == "..")
                {
                    // sb == ".." && sa == "."
                    // sb == ".." && sa == ".."
                    // sb == "." && sa == ".."
                    // are all handled above, so the
                    // only resulting case here is
                    // if sa is a normal filename.
                    return 1;
                }

                if (Path.GetExtension(sa).Equals(Path.GetExtension(sb), StringComparison.InvariantCultureIgnoreCase))
                {
                    return _compareInfo.Compare(
                        Path.GetFileNameWithoutExtension(sa),
                        Path.GetFileNameWithoutExtension(sb),
                        CompareOptions.IgnoreCase);
                }
                else
                {
                    return _compareInfo.Compare(
                        sa,
                        sb,
                        CompareOptions.IgnoreCase);
                }
            }
            else
            {
                return Comparer.Default.Compare(a, b);
            }
        }

        /// <summary>
        /// Returns true if two filenames are the same.
        /// </summary>
        /// <param name="x">The first filename.</param>
        /// <param name="y">The second filename.</param>
        /// <returns>Whether the filenames are equal.</returns>
        public bool Equals(string? x, string? y)
        {
            return Compare(x, y) == 0;
        }

        /// <summary>
        /// Returns the hashcode for a filename.
        /// </summary>
        /// <param name="obj">The filename.</param>
        /// <returns>The hash code.</returns>
        public int GetHashCode([DisallowNull] string obj)
        {
            return obj.ToLowerInvariant().GetHashCode();
        }
    }
}