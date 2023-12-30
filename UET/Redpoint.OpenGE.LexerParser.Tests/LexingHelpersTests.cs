namespace Redpoint.OpenGE.LexerParser.Tests
{
    using Redpoint.OpenGE.LexerParser;
    using Xunit;

    public class LexingHelpersTests
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
        [InlineData(-1, "\\\n/**/")]
        [InlineData(-1, "/\\\n**/")]
        [InlineData(-1, "/*\\\n*/")]
        [InlineData(-1, "/**\\\n/")]
        [InlineData(-1, "/**/\\\n")]
        [InlineData(-1, "  \\\n \t\t \\\n/\\\n*\\\n*\\\n/\\\n \\\n \t\t")]
        [InlineData(28, "  \\\n \t\t \\\n/\\\n*\\\n*\\\n/\\\n \\\n \t\tTEST")]
        public void IndexOfFirstNonWhitespaceNonCommentCharacter(int expected, string test)
        {
            Assert.Equal(expected, LexingHelpers.IndexOfFirstNonWhitespaceNonCommentCharacter(test));
        }

        [Theory]
        [InlineData("", "")]
        [InlineData("", "*")]
        [InlineData("", " ")]
        [InlineData("_", "_")]
        [InlineData("a", "a")]
        [InlineData("a", "a ")]
        [InlineData("b", "b")]
        [InlineData("b", "b ")]
        [InlineData("z", "z")]
        [InlineData("z", "z ")]
        [InlineData("A", "A")]
        [InlineData("A", "A ")]
        [InlineData("B", "B")]
        [InlineData("B", "B ")]
        [InlineData("Z", "Z")]
        [InlineData("Z", "Z ")]
        [InlineData("", "0")]
        [InlineData("", "0 ")]
        [InlineData("", "1")]
        [InlineData("", "1 ")]
        [InlineData("", "9")]
        [InlineData("", "9 ")]
        [InlineData("A0", "A0")]
        [InlineData("A0", "A0 ")]
        [InlineData("B1", "B1")]
        [InlineData("B1", "B1 ")]
        [InlineData("Z9", "Z9")]
        [InlineData("Z9", "Z9 ")]
        [InlineData("helloWorld", "helloWorld thenAnotherThing ")]
        [InlineData("otherIdentifier0", "otherIdentifier0 thenAnotherThing ")]
        [InlineData("_SOMEWORD99", "_SOMEWORD99 thenAnotherThing ")]
        [InlineData("multiline0Identifier", "mult\\\niline\\\n0Identifier")]
        [InlineData("multiline0Identifier", "\\\nmultiline\\\n0Identifier")]
        public void ConsumeWord(string expected, string test)
        {
            ReadOnlySpan<char> span = test.AsSpan();
            Assert.Equal(expected, LexingHelpers.ConsumeWord(ref span).ToString());
        }
    }
}
