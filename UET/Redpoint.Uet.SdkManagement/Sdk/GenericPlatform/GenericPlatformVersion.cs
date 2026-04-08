namespace Redpoint.Uet.SdkManagement.Sdk.GenericPlatform
{
    using System;
    using System.Collections.Generic;
    using System.Numerics;
    using System.Text;
    using System.Text.RegularExpressions;

    internal class GenericPlatformVersion
    {
        public long[] Components = [];
        public long Major => Components.Length > 0 ? Components[0] : 0;
        public long Minor => Components.Length > 1 ? Components[1] : 0;
        public long Patch => Components.Length > 2 ? Components[2] : 0;
        public long Hotfix => Components.Length > 3 ? Components[3] : 0;

        public readonly static Regex VersionRegex = new("^(?<major>[0-9]+)(\\.(?<minor>[0-9]+)(\\.(?<patch>[0-9]+)(\\.(?<hotfix>[0-9]+))?)?)?$");

        public override string ToString()
        {
            if (Components.Length >= 4)
            {
                return $"{Major}.{Minor}.{Patch}.{Hotfix}";
            }
            else if (Components.Length >= 3)
            {
                return $"{Major}.{Minor}.{Patch}";
            }
            else if (Components.Length >= 2)
            {
                return $"{Major}.{Minor}";
            }
            else
            {
                return $"{Major}";
            }
        }

        public static GenericPlatformVersion? Parse(string version)
        {
            var match = VersionRegex.Match(version);
            if (!match.Success)
            {
                return null;
            }
            var hasMajor = long.TryParse(match.Groups["major"].Value, out var major);
            var hasMinor = long.TryParse(match.Groups["minor"].Value, out var minor);
            var hasPatch = long.TryParse(match.Groups["patch"].Value, out var patch);
            var hasHotfix = long.TryParse(match.Groups["hotfix"].Value, out var hotfix);

            if (hasMajor && hasMinor && hasPatch && hasHotfix)
            {
                return new GenericPlatformVersion { Components = [major, minor, patch, hotfix] };
            }
            else if (hasMajor && hasMinor && hasPatch)
            {
                return new GenericPlatformVersion { Components = [major, minor, patch] };
            }
            else if (hasMajor && hasMinor)
            {
                return new GenericPlatformVersion { Components = [major, minor] };
            }
            else if (hasMajor)
            {
                return new GenericPlatformVersion { Components = [major] };
            }
            else
            {
                return null;
            }
        }

        public static long operator -(GenericPlatformVersion a, GenericPlatformVersion b)
        {
            return
                ((a.Major - b.Major) * 1_000_000_000L) +
                ((a.Minor - b.Minor) * 1_000_000L) +
                ((a.Patch - b.Patch) * 1_000L) +
                ((a.Hotfix - b.Hotfix) * 1L);
        }

        public static bool IsCandidateWithinBounds(GenericPlatformVersion candidate, GenericPlatformVersion min, GenericPlatformVersion max)
        {
            return candidate - min >= 0 && max - candidate >= 0;
        }
    }
}
