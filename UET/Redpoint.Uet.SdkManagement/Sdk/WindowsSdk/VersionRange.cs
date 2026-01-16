namespace Redpoint.Uet.SdkManagement.AutoSdk.WindowsSdk
{
    using System.Globalization;

    internal class VersionRange
    {
        public required VersionNumber Min { get; init; }

        public required VersionNumber Max { get; init; }

        public static VersionRange Parse(string version)
        {
            var components = version.Split("-");
            return new VersionRange
            {
                Min = VersionNumber.Parse(components[0]),
                Max = VersionNumber.Parse(components[1]),
            };
        }

        public bool Contains(VersionNumber versionNumber)
        {
            return versionNumber >= Min && versionNumber <= Max;
        }
    }
}
