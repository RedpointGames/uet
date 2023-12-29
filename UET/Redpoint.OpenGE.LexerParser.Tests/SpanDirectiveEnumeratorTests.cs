namespace Redpoint.OpenGE.LexerParser.Tests
{
    using Redpoint.OpenGE.LexerParser.LineScanning;
    using Xunit;

    public class SpanDirectiveEnumeratorTests
    {
        [Theory]
        [InlineData(-1, "")]
        [InlineData(-1, "/**/")]
        [InlineData(-1, " ")]
        [InlineData(-1, "\t")]
        [InlineData(-1, "\t\t  \t\t  ")]
        [InlineData(-1, "  \t\t  \t\t")]
        [InlineData(-1, "  \t\t  \t\t  ")]
        [InlineData(-1, "\t\t  \t\t")]
        [InlineData(3, "\t\t \n \t\t")]
        [InlineData(-1, "\t/*\t \n \t*/\t")]
        [InlineData(-1, "/*\n*/")]
        [InlineData(-1, "/**/\t/**/")]
        [InlineData(4, "/**/\n/**/")]
        [InlineData(-1, "/******/")]
        [InlineData(-1, "/*************************/")]
        [InlineData(-1, "/*///////////////////////*/")]
        [InlineData(-1, "/* /* /* /* /* /* /* /* */")]
        [InlineData(-1, "/*\n*\n*\n*\n*\n*/")]
        [InlineData(0, "a")]
        [InlineData(0, "#")]
        [InlineData(1, "/")]
        [InlineData(2, "/*")]
        [InlineData(3, "/**")]
        [InlineData(4, "/**a")]
        [InlineData(4, "/***")]
        [InlineData(4, "/** ")]
        [InlineData(5, "/* \n\n")]
        public void IndexOfFirstNonWhitespaceNonCommentCharacter(int expected, string test)
        {
            Assert.Equal(expected, SpanDirectiveEnumerator.IndexOfFirstNonWhitespaceNonCommentCharacter(test));
        }
    }
}
