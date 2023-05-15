namespace Redpoint.SdkManagement.WindowsSdk
{
    internal class VersionNumber
    {
        public int Major { get; init; }

        public int Minor { get; init; }

        public int Patch { get; init; }

        public static VersionNumber Parse(string version)
        {
            var components = version.Split(".");
            return new VersionNumber
            {
                Major = int.Parse(components[0]),
                Minor = int.Parse(components[1]),
                Patch = int.Parse(components[2]),
            };
        }

        public static bool operator ==(VersionNumber a, VersionNumber b)
        {
            return a.Major == b.Major && a.Minor == b.Minor && a.Patch == b.Patch;
        }

        public static bool operator !=(VersionNumber a, VersionNumber b)
        {
            return !(a == b);
        }

        public static bool operator <(VersionNumber a, VersionNumber b)
        {
            return
                a.Major < b.Major ||
                (a.Major == b.Major && a.Minor < b.Minor) ||
                (a.Major == b.Major && a.Minor == b.Minor && a.Patch < b.Patch);
        }

        public static bool operator >(VersionNumber a, VersionNumber b)
        {
            return
                a.Major > b.Major ||
                (a.Major == b.Major && a.Minor > b.Minor) ||
                (a.Major == b.Major && a.Minor == b.Minor && a.Patch > b.Patch);
        }

        public static bool operator <=(VersionNumber a, VersionNumber b)
        {
            return
                a.Major < b.Major ||
                (a.Major == b.Major && a.Minor < b.Minor) ||
                (a.Major == b.Major && a.Minor == b.Minor && a.Patch <= b.Patch);
        }

        public static bool operator >=(VersionNumber a, VersionNumber b)
        {
            return
                a.Major > b.Major ||
                (a.Major == b.Major && a.Minor > b.Minor) ||
                (a.Major == b.Major && a.Minor == b.Minor && a.Patch >= b.Patch);
        }

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (ReferenceEquals(obj, null))
            {
                return false;
            }

            if (obj is VersionNumber other)
            {
                return this == other;
            }

            return false;
        }

        public override int GetHashCode()
        {
            return Major.GetHashCode() ^
                Minor.GetHashCode() ^
                Patch.GetHashCode();
        }

        public override string ToString()
        {
            return $"{Major}.{Minor}.{Patch}";
        }
    }
}
