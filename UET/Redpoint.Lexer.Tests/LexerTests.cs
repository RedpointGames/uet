namespace Redpoint.Lexer.Tests
{
    using System;
    using Xunit;

    public class LexerTests
    {
        [Theory]
        [InlineData("", "")]
        [InlineData("", "t")]
        [InlineData("", "te")]
        [InlineData("", "tes")]
        [InlineData("test", "test")]
        [InlineData("test", "test2")]
        [InlineData("test", "testtest")]
        [InlineData("", "te\\\nst")]
        public void ConsumeTest(string expected, string input)
        {
            var cursor = new LexerCursor();
            var span = input.AsSpan();
            var result = TestLexer.ConsumeTest(ref span, ref cursor);
            Assert.Equal(expected, result.ToString());
        }

        [Theory]
        [InlineData("", "")]
        [InlineData("", "t")]
        [InlineData("", "te")]
        [InlineData("", "tes")]
        [InlineData("test", "test")]
        [InlineData("test", "test2")]
        [InlineData("test", "testtest")]
        [InlineData("te\\\nst", "te\\\nst")]
        public void ConsumeTestWithNewlines(string expected, string input)
        {
            var cursor = new LexerCursor();
            var span = input.AsSpan();
            var result = TestLexer.ConsumeTestWithNewlines(ref span, ref cursor);
            Assert.Equal(expected, result.Span.ToString());
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
        [InlineData("mult", "mult\\\niline\\\n0Identifier")]
        [InlineData("", "\\\nmultiline\\\n0Identifier")]
        public void ConsumeWord(string expected, string test)
        {
            var cursor = new LexerCursor();
            var span = test.AsSpan();
            var result = TestLexer.ConsumeWord(ref span, ref cursor);
            Assert.Equal(expected, result.ToString());
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
        [InlineData("mult\\\niline\\\n0Identifier", "mult\\\niline\\\n0Identifier")]
        [InlineData("\\\nmultiline\\\n0Identifier", "\\\nmultiline\\\n0Identifier")]
        public void ConsumeWordWithNewlines(string expected, string test)
        {
            var cursor = new LexerCursor();
            var span = test.AsSpan();
            var result = TestLexer.ConsumeWordWithNewlines(ref span, ref cursor);
            Assert.Equal(expected, result.Span.ToString());
        }
    }
}
