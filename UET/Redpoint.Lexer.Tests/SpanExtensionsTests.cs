namespace Redpoint.Lexer.Tests
{
    using System;
    using System.Text;
    using Xunit;

    public class SpanExtensionsTests
    {
        [Theory]
        [InlineData("", 0, "", 0)]
        [InlineData("test", 0, "test", 0)]
        [InlineData("est", 1, "test", 1)]
        [InlineData("st", 2, "test", 2)]
        [InlineData("t", 3, "test", 3)]
        [InlineData("", 4, "test", 4)]
        public void Consume(string expectedResult, int expectedConsumed, string source, int consume)
        {
            {
                var actualResult = source.AsSpan();
                LexerCursor actualConsumed = default;
                actualResult.ConsumeUtf16(consume, ref actualConsumed);
                Assert.Equal(expectedResult, actualResult.ToString());
                Assert.Equal(expectedConsumed, actualConsumed.CharactersConsumed);
            }

            {
                var expectedResultUtf8 = new ReadOnlySpan<byte>(Encoding.UTF8.GetBytes(expectedResult));
                var actualResultUtf8 = new ReadOnlySpan<byte>(Encoding.UTF8.GetBytes(source));
                LexerCursor actualConsumedUtf8 = default;
                actualResultUtf8.ConsumeUtf8(consume, ref actualConsumedUtf8);
                Assert.Equal(expectedResult, actualResultUtf8.ToString());
                Assert.Equal(expectedConsumed, actualConsumedUtf8.CharactersConsumed);
            }
        }

        [Theory]
        [InlineData("", 1)]
        [InlineData("test", -1)]
        [InlineData("test", 5)]
        public void ConsumeThrows(string source, int consume)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                var actualResult = source.AsSpan();
                LexerCursor actualConsumed = default;
                actualResult.ConsumeUtf16(consume, ref actualConsumed);
            });
        }

        [Theory]
        [InlineData("", 0, "")]
        [InlineData("\\", 0, "\\")]
        [InlineData("", 2, "\\\n")]
        [InlineData("", 3, "\\\r\n")]
        [InlineData("", 4, "\\\n\\\n")]
        [InlineData("test", 4, "\\\n\\\ntest")]
        [InlineData("", 5, "\\\n\\\r\n")]
        [InlineData("test", 5, "\\\n\\\r\ntest")]
        [InlineData("", 6, "\\\r\n\\\r\n")]
        [InlineData("test", 6, "\\\r\n\\\r\ntest")]
        [InlineData("test\\\r\n\\\r\n", 0, "test\\\r\n\\\r\n")]
        public void ConsumeNewlineContinuations(string expectedResult, int expectedConsumed, string source)
        {
            var actualResult = source.AsSpan();
            LexerCursor actualConsumed = default;
            actualResult.ConsumeNewlineContinuationsUtf16(ref actualConsumed);
            Assert.Equal(expectedResult, actualResult.ToString());
            Assert.Equal(expectedConsumed, actualConsumed.CharactersConsumed);
        }

        [Theory]
        [InlineData(-1, "", 0)]
        [InlineData(-1, "test", 0)]
        [InlineData(1, "test", 2)]
        [InlineData(1, "te\\\nst", 4)]
        [InlineData(2, "te\\\nst", 3)]
        public void IndexOfAnyBeforeNewlineContinuations(int expectedPosition, string source, int startPosition)
        {
            var span = source.AsSpan();
            var actualPosition = span.IndexOfAnyBeforeNewlineContinuationsUtf16(startPosition);
            Assert.Equal(expectedPosition, actualPosition);
        }

        [Theory]
        [InlineData(true, "", 0, "", "")]
        [InlineData(true, "source", 0, "source", "")]
        [InlineData(false, "", 0, "", "sequence")]
        [InlineData(false, "source", 0, "source", "sequence")]
        [InlineData(true, "*/", 2, "/**/", "/*")]
        [InlineData(true, "*/", 4, "\\\n/**/", "/*")]
        [InlineData(true, "*/", 4, "/\\\n**/", "/*")]
        [InlineData(true, "\\\n*/", 2, "/*\\\n*/", "/*")]
        [InlineData(true, "*\\\n/", 2, "/**\\\n/", "/*")]
        [InlineData(true, "*/\\\n", 2, "/**/\\\n", "/*")]
        public void TryConsumeSequence(bool expectedOutcome, string expectedResult, int expectedConsumed, string source, string sequence)
        {
            var actualResult = source.AsSpan();
            LexerCursor actualConsumed = default;
            var containsNewlineContinuations = false;
            var actualOutcome = actualResult.TryConsumeSequenceUtf16(sequence, ref actualConsumed, ref containsNewlineContinuations);
            Assert.Equal(expectedOutcome, actualOutcome);
            Assert.Equal(expectedResult, actualResult.ToString());
            Assert.Equal(expectedConsumed, actualConsumed.CharactersConsumed);
        }
    }
}
