namespace Redpoint.Uet.SdkManagement.Tests
{
    using Redpoint.Uet.SdkManagement.Sdk.GenericPlatform;
    using Redpoint.Uet.SdkManagement.Sdk.VersionNumbers;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Text;

    public class MacVersionTestDataGenerator : IEnumerable<object?[]>
    {
        private readonly List<object?[]> _data = new List<object?[]>
        {
            new object?[] { null, "15.2", "15.2.0", "26.9.0", Array.Empty<string>() },
            new object?[] { "15.2", "15.2", "15.2.0", "26.9.0", new[] { "Xcode_15.2.xip" } },
            new object?[] { "15.2", "15.2", "15.2.0", "26.9.0", new[] { "Xcode_15.2.xip", "Xcode_15.4.xip", "Xcode_16.0.xip", "Xcode_16.3.xip" } },
            new object?[] { "16.3", null, "15.2.0", "26.9.0", new[] { "Xcode_15.2.xip", "Xcode_15.4.xip", "Xcode_16.0.xip", "Xcode_16.3.xip" } },
            new object?[] { "15.4", "15.3", "15.2.0", "26.9.0", new[] { "Xcode_15.2.xip", "Xcode_15.4.xip", "Xcode_16.0.xip", "Xcode_16.3.xip" } },
            new object?[] { "16.0", null, "15.2.0", "16.2.0", new[] { "Xcode_15.2.xip", "Xcode_15.4.xip", "Xcode_16.0.xip", "Xcode_16.3.xip" } },
            new object?[] { "16.3", null, "16.1", "26.9.0", new[] { "Xcode_15.2.xip", "Xcode_15.4.xip", "Xcode_16.0.xip", "Xcode_16.3.xip" } },
            new object?[] { "16.0", "16.1", "15.9", "26.9.0", new[] { "Xcode_15.2.xip", "Xcode_15.4.xip", "Xcode_16.0.xip", "Xcode_16.3.xip" } },
        };

        public IEnumerator<object[]> GetEnumerator() => _data.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public class MacVersionTests
    {
        [Theory]
        [ClassData(typeof(MacVersionTestDataGenerator))]
        public void GetBestAvailableVersionFromAvailableXcodes(string? expectedVersion, string? mainVersion, string? minVersion, string? maxVersion, string[] availableXcodes)
        {
            var selectedVersion = JsonMacVersionNumbers.GetBestAvailableVersionFromAvailableXcodes(
                mainVersion != null ? GenericPlatformVersion.Parse(mainVersion) : null,
                minVersion != null ? GenericPlatformVersion.Parse(minVersion) : null,
                maxVersion != null ? GenericPlatformVersion.Parse(maxVersion) : null,
                availableXcodes);
            if (expectedVersion == null)
            {
                Assert.Null(selectedVersion);
            }
            else
            {
                Assert.NotNull(selectedVersion);
                Assert.Equal(expectedVersion, selectedVersion.ToString());
            }
        }
    }
}
