namespace Redpoint.OpenGE.Component.Worker.Tests
{
    using Redpoint.OpenGE.Component.Worker.PchPortability;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;

    public class PchTests
    {
        [SkippableFact]
        public async Task TestPchLocationDetection()
        {
            var testPath = @"C:\Work\internal\EOS_OSB\EOS_OSB\Intermediate\Build\Win64\x64\ExampleOSSEditor\DebugGame\Core\SharedPCH.Core.NonOptimized.ShadowErrors.h.pch";

            Skip.IfNot(OperatingSystem.IsWindows());
            Skip.IfNot(File.Exists(testPath));

            var pchPortability = new DefaultPchPortability();
            var locations = await pchPortability.ScanPchForReplacementLocationsAsync(
                testPath,
                @"C:\Work\internal",
                CancellationToken.None);
            Assert.Equal(@"C:\Work\internal".Length, locations.PortablePathPrefixLength);
            Assert.Equal(8, locations.ReplacementOffsets.Count);
        }
    }
}
