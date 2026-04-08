namespace Redpoint.Uet.SdkManagement.Tests
{
    using Redpoint.Uet.SdkManagement.Sdk.GenericPlatform;
    using System;
    using System.Collections.Generic;
    using System.Text;

    public class GenericPlatformVersionTests
    {
        [Fact]
        public void TestVersions()
        {
            Assert.Equal(0, GenericPlatformVersion.Parse("1.0.0")! - GenericPlatformVersion.Parse("1.0.0")!);
            Assert.Equal(1_000_000_000, GenericPlatformVersion.Parse("2.0.0")! - GenericPlatformVersion.Parse("1.0.0")!);
            Assert.Equal(1_000, GenericPlatformVersion.Parse("1.0.1")! - GenericPlatformVersion.Parse("1.0.0")!);
            Assert.True(GenericPlatformVersion.IsCandidateWithinBounds(
                GenericPlatformVersion.Parse("1.0.1")!,
                GenericPlatformVersion.Parse("1.0.0")!,
                GenericPlatformVersion.Parse("2.0.0")!));
            Assert.True(GenericPlatformVersion.IsCandidateWithinBounds(
                GenericPlatformVersion.Parse("1.0.0")!,
                GenericPlatformVersion.Parse("1.0.0")!,
                GenericPlatformVersion.Parse("2.0.0")!));
            Assert.True(GenericPlatformVersion.IsCandidateWithinBounds(
                GenericPlatformVersion.Parse("1.1.0")!,
                GenericPlatformVersion.Parse("1.0.0")!,
                GenericPlatformVersion.Parse("2.0.0")!));
            Assert.True(GenericPlatformVersion.IsCandidateWithinBounds(
                GenericPlatformVersion.Parse("2.0.0")!,
                GenericPlatformVersion.Parse("1.0.0")!,
                GenericPlatformVersion.Parse("2.0.0")!));
        }
    }
}
