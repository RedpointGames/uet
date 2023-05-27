namespace Redpoint.Vfs.Abstractions
{
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;

    public class FileSystemNameComparer : IComparer<string>, IEqualityComparer<string>
    {
        private CompareInfo _compareInfo = CultureInfo.InvariantCulture.CompareInfo;

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

                return _compareInfo.Compare(sa, sb, CompareOptions.IgnoreCase);
            }
            else
            {
                return Comparer.Default.Compare(a, b);
            }
        }

        public bool Equals(string? x, string? y)
        {
            return Compare(x, y) == 0;
        }

        public int GetHashCode([DisallowNull] string obj)
        {
            return obj.ToLowerInvariant().GetHashCode();
        }
    }
}