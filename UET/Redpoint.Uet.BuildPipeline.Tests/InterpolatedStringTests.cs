namespace Redpoint.Uet.BuildPipeline.Tests
{
    using Redpoint.Uet.BuildPipeline.MultiWorkspace;

    public class InterpolatedStringTests
    {
        [Fact]
        public void InterpolatedStringWorks()
        {
            var i = new InterpolatedString("abc_${env:INTERPOLATED_STRING_TEST}");

            Assert.Equal(
                "abc_${env:INTERPOLATED_STRING_TEST}",
                i.PatternValue);
            Assert.Equal(
                "abc_",
                i.EvaluatedString);

            System.Environment.SetEnvironmentVariable("INTERPOLATED_STRING_TEST", "abc");

            Assert.Equal(
                "abc_abc",
                i.PatternValue);
        }
    }
}