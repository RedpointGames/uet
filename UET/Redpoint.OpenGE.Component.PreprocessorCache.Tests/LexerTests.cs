namespace Redpoint.OpenGE.Component.PreprocessorCache.Tests
{
    using Redpoint.OpenGE.Component.PreprocessorCache.LexerParser;
    using Redpoint.OpenGE.Protocol;

    public class LexerTests
    {
        private static void AssertType(PreprocessorExpressionToken token, PreprocessorExpressionTokenType type)
        {
            Assert.Equal(PreprocessorExpressionToken.DataOneofCase.Type, token.DataCase);
            Assert.Equal(type, token.Type);
        }

        private static void AssertNumber(PreprocessorExpressionToken token, long number)
        {
            Assert.Equal(PreprocessorExpressionToken.DataOneofCase.Number, token.DataCase);
            Assert.Equal(number, token.Number);
        }

        private static void AssertIdentifier(PreprocessorExpressionToken token, string val)
        {
            Assert.Equal(PreprocessorExpressionToken.DataOneofCase.Identifier, token.DataCase);
            Assert.Equal(val, token.Identifier);
        }

        private static void AssertText(PreprocessorExpressionToken token, string val)
        {
            Assert.Equal(PreprocessorExpressionToken.DataOneofCase.Text, token.DataCase);
            Assert.Equal(val, token.Text);
        }

        private static void AssertWhitespace(PreprocessorExpressionToken token)
        {
            Assert.Equal(PreprocessorExpressionToken.DataOneofCase.Whitespace, token.DataCase);
        }

        [Fact]
        public void LexSimpleExpression()
        {
            var results = PreprocessorExpressionLexer.Lex("4+5").ToArray();
            Assert.Equal(3, results.Length);
            AssertNumber(results[0], 4);
            AssertType(results[1], PreprocessorExpressionTokenType.Add);
            AssertNumber(results[2], 5);
        }

        [Fact]
        public void LexUnsignedLongExpression()
        {
            var results = PreprocessorExpressionLexer.Lex("0xFFFFFFFFUL").ToArray();
            Assert.Single(results);
            AssertNumber(results[0], unchecked((long)0xFFFFFFFFUL));
        }

        [Fact]
        public void LexIntelExpression()
        {
            var results = PreprocessorExpressionLexer.Lex("(__INTEL_COMPILER && (__INTEL_COMPILER < 1500 || __INTEL_COMPILER == 1500 && __INTEL_COMPILER_UPDATE <= 1))").ToArray();
        }

        [Fact]
        public void LexInvocationExpression()
        {
            var results = PreprocessorExpressionLexer.Lex("PREPROCESSOR_TO_STRING(PREPROCESSOR_JOIN(PLATFORM_HEADER_NAME/PLATFORM_HEADER_NAME, Suffix))").ToArray();
            Assert.Equal(12, results.Length);
            AssertIdentifier(results[0], "PREPROCESSOR_TO_STRING");
            AssertType(results[1], PreprocessorExpressionTokenType.ParenOpen);
            AssertIdentifier(results[2], "PREPROCESSOR_JOIN");
            AssertType(results[3], PreprocessorExpressionTokenType.ParenOpen);
            AssertIdentifier(results[4], "PLATFORM_HEADER_NAME");
            AssertType(results[5], PreprocessorExpressionTokenType.Divide);
            AssertIdentifier(results[6], "PLATFORM_HEADER_NAME");
            AssertType(results[7], PreprocessorExpressionTokenType.Comma);
            AssertWhitespace(results[8]);
            AssertIdentifier(results[9], "Suffix");
            AssertType(results[10], PreprocessorExpressionTokenType.ParenClose);
            AssertType(results[11], PreprocessorExpressionTokenType.ParenClose);

            results = PreprocessorExpressionLexer.Lex("PREPROCESSOR_JOIN_INNER(x, y)").ToArray();
            Assert.Equal(7, results.Length);
            AssertIdentifier(results[0], "PREPROCESSOR_JOIN_INNER");
            AssertType(results[1], PreprocessorExpressionTokenType.ParenOpen);
            AssertIdentifier(results[2], "x");
            AssertType(results[3], PreprocessorExpressionTokenType.Comma);
            AssertWhitespace(results[4]);
            AssertIdentifier(results[5], "y");
            AssertType(results[6], PreprocessorExpressionTokenType.ParenClose);

            results = PreprocessorExpressionLexer.Lex("x##y").ToArray();
            Assert.Equal(3, results.Length);
            AssertIdentifier(results[0], "x");
            AssertType(results[1], PreprocessorExpressionTokenType.Join);
            AssertIdentifier(results[2], "y");

            results = PreprocessorExpressionLexer.Lex("PREPROCESSOR_TO_STRING_INNER(x)").ToArray();
            Assert.Equal(4, results.Length);
            AssertIdentifier(results[0], "PREPROCESSOR_TO_STRING_INNER");
            AssertType(results[1], PreprocessorExpressionTokenType.ParenOpen);
            AssertIdentifier(results[2], "x");
            AssertType(results[3], PreprocessorExpressionTokenType.ParenClose);

            results = PreprocessorExpressionLexer.Lex("#x").ToArray();
            Assert.Equal(2, results.Length);
            AssertType(results[0], PreprocessorExpressionTokenType.Stringify);
            AssertIdentifier(results[1], "x");
        }

        [Fact]
        public void LexStringEscape()
        {
            var results = PreprocessorExpressionLexer.Lex("\"te+st\\\"\"").ToArray();
            Assert.Single(results);
            AssertText(results[0], "\"te+st\\\"\"");
        }

        [Fact]
        public void LexTwoStrings()
        {
            var results = PreprocessorExpressionLexer.Lex("\"a\" \"b\"").ToArray();
            Assert.Equal(3, results.Length);
            AssertText(results[0], "\"a\"");
            AssertWhitespace(results[1]);
            AssertText(results[2], "\"b\"");
        }

        [Fact]
        public void LexZero()
        {
            var results = PreprocessorExpressionLexer.Lex("0").ToArray();
            Assert.Single(results);
            AssertNumber(results[0], 0);
        }
    }
}