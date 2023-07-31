namespace Redpoint.OpenGE.PreprocessorCache.Tests
{
    using PreprocessorCacheApi;
    using Redpoint.OpenGE.PreprocessorCache.LexerParser;
    using System.Linq;

    public class ParserExpansionTests
    {
        private PreprocessorExpression[] AssertChain(PreprocessorExpression result, int expectedLength)
        {
            if (expectedLength == 1)
            {
                return new[] { result };
            }
            else
            {
                Assert.Equal(PreprocessorExpression.ExprOneofCase.Chain, result.ExprCase);
                return result.Chain.Expressions.ToArray();
            }
        }

        private PreprocessorExpression[] AssertInvocation(PreprocessorExpression result, string identifier)
        {
            Assert.Equal(PreprocessorExpression.ExprOneofCase.Invoke, result.ExprCase);
            Assert.Equal(identifier, result.Invoke.Identifier);
            return result.Invoke.Arguments.ToArray();
        }

        private void AssertIdentifier(PreprocessorExpression value, string identifier)
        {
            Assert.Equal(PreprocessorExpression.ExprOneofCase.Token, value.ExprCase);
            Assert.Equal(PreprocessorExpressionToken.DataOneofCase.Identifier, value.Token.DataCase);
            Assert.Equal(identifier, value.Token.Identifier);
        }

        private void AssertWhitespace(PreprocessorExpression value, string whitespace)
        {
            Assert.Equal(PreprocessorExpression.ExprOneofCase.Whitespace, value.ExprCase);
            Assert.Equal(whitespace, value.Whitespace);
        }

        private void AssertText(PreprocessorExpression value, string identifier)
        {
            Assert.Equal(PreprocessorExpression.ExprOneofCase.Token, value.ExprCase);
            Assert.Equal(PreprocessorExpressionToken.DataOneofCase.Text, value.Token.DataCase);
            Assert.Equal(identifier, value.Token.Text);
        }

        [Fact]
        public void ParseSimpleExpansion()
        {
            var result = PreprocessorExpressionParser.ParseExpansion(
                PreprocessorExpressionLexer.Lex("hello world"));
            var chain = AssertChain(result, 3);
            AssertIdentifier(chain[0], "hello");
            AssertWhitespace(chain[1], " ");
            AssertIdentifier(chain[2], "world");
        }

        [Fact]
        public void ParseTokenExpansion()
        {
            var result = PreprocessorExpressionParser.ParseExpansion(
                PreprocessorExpressionLexer.Lex("PLATFORM_HEADER_NAME/PLATFORM_HEADER_NAME"));
            var chain = AssertChain(result, 3);
            AssertIdentifier(chain[0], "PLATFORM_HEADER_NAME");
            AssertText(chain[1], "/");
            AssertIdentifier(chain[2], "PLATFORM_HEADER_NAME");
        }

        [Fact]
        public void ParseTokenWithWhitespaceExpansion()
        {
            var result = PreprocessorExpressionParser.ParseExpansion(
                PreprocessorExpressionLexer.Lex("PLATFORM_HEADER_NAME\t/   PLATFORM_HEADER_NAME"));
            var chain = AssertChain(result, 5);
            AssertIdentifier(chain[0], "PLATFORM_HEADER_NAME");
            AssertWhitespace(chain[1], "\t");
            AssertText(chain[2], "/");
            AssertWhitespace(chain[3], "   ");
            AssertIdentifier(chain[4], "PLATFORM_HEADER_NAME");
        }

        [Fact]
        public void ParseTokenWithNewlineExpansion()
        {
            var result = PreprocessorExpressionParser.ParseExpansion(
                PreprocessorExpressionLexer.Lex("PLATFORM_HEADER_NAME\n/\nPLATFORM_HEADER_NAME"));
            var chain = AssertChain(result, 5);
            AssertIdentifier(chain[0], "PLATFORM_HEADER_NAME");
            AssertWhitespace(chain[1], "\n");
            AssertText(chain[2], "/");
            AssertWhitespace(chain[3], "\n");
            AssertIdentifier(chain[4], "PLATFORM_HEADER_NAME");
        }

        [Fact]
        public void ParseInvocationExpansion()
        {
            var result = PreprocessorExpressionParser.ParseExpansion(
                PreprocessorExpressionLexer.Lex("FUNC(SIMPLE)"));
            var arguments = AssertInvocation(result, "FUNC");
            Assert.Single(arguments);
            AssertIdentifier(arguments[0], "SIMPLE");
        }

        [Fact]
        public void ParseInvocationWithMultipleArgumentsExpansion()
        {
            var result = PreprocessorExpressionParser.ParseExpansion(
                PreprocessorExpressionLexer.Lex("FUNC(A,B,C,D)"));
            var arguments = AssertInvocation(result, "FUNC");
            Assert.Equal(4, arguments.Length);
            AssertIdentifier(arguments[0], "A");
            AssertIdentifier(arguments[1], "B");
            AssertIdentifier(arguments[2], "C");
            AssertIdentifier(arguments[3], "D");
        }

        [Fact]
        public void ParseInvocationWithMultipleSpacedArgumentsExpansion()
        {
            var result = PreprocessorExpressionParser.ParseExpansion(
                PreprocessorExpressionLexer.Lex("FUNC( A , B , C , D )"));
            var arguments = AssertInvocation(result, "FUNC");
            Assert.Equal(4, arguments.Length);
            void AssertSpacedIdentifier(PreprocessorExpression expression, string identifier)
            {
                var chain = AssertChain(expression, 3);
                AssertWhitespace(chain[0], " ");
                AssertIdentifier(chain[1], identifier);
                AssertWhitespace(chain[2], " ");
            }
            AssertSpacedIdentifier(arguments[0], "A");
            AssertSpacedIdentifier(arguments[1], "B");
            AssertSpacedIdentifier(arguments[2], "C");
            AssertSpacedIdentifier(arguments[3], "D");
        }

        [Fact]
        public void ParseInvocationWithMultipleParenthesisedArgumentsExpansion()
        {
            var result = PreprocessorExpressionParser.ParseExpansion(
                PreprocessorExpressionLexer.Lex("FUNC((A),(B),(C),(D))"));
            var arguments = AssertInvocation(result, "FUNC");
            Assert.Equal(4, arguments.Length);
            void AssertParenthesisedIdentifier(PreprocessorExpression expression, string identifier)
            {
                var chain = AssertChain(expression, 3);
                AssertText(chain[0], "(");
                AssertIdentifier(chain[1], identifier);
                AssertText(chain[2], ")");
            }
            AssertParenthesisedIdentifier(arguments[0], "A");
            AssertParenthesisedIdentifier(arguments[1], "B");
            AssertParenthesisedIdentifier(arguments[2], "C");
            AssertParenthesisedIdentifier(arguments[3], "D");
        }

        [Fact]
        public void ParseInvocationWithMultipleInvocationArgumentsExpansion()
        {
            var result = PreprocessorExpressionParser.ParseExpansion(
                PreprocessorExpressionLexer.Lex("FUNC(A(),B(),C(),D())"));
            var arguments = AssertInvocation(result, "FUNC");
            Assert.Equal(4, arguments.Length);
            void AssertInvocationIdentifier(PreprocessorExpression expression, string identifier)
            {
                var arguments = AssertInvocation(expression, identifier);
                Assert.Empty(arguments);
            }
            AssertInvocationIdentifier(arguments[0], "A");
            AssertInvocationIdentifier(arguments[1], "B");
            AssertInvocationIdentifier(arguments[2], "C");
            AssertInvocationIdentifier(arguments[3], "D");
        }

        [Fact]
        public void ParseNestedParenthesisExpansion()
        {
            var result = PreprocessorExpressionParser.ParseExpansion(
                PreprocessorExpressionLexer.Lex("(A)* ((B )!=( (E)) )"));
            var chain = AssertChain(result, 19);
            var i = 0;
            AssertText(chain[i++], "(");
            AssertIdentifier(chain[i++], "A");
            AssertText(chain[i++], ")");
            AssertText(chain[i++], "*");
            AssertWhitespace(chain[i++], " ");
            AssertText(chain[i++], "(");
            AssertText(chain[i++], "(");
            AssertIdentifier(chain[i++], "B");
            AssertWhitespace(chain[i++], " ");
            AssertText(chain[i++], ")");
            AssertText(chain[i++], "!=");
            AssertText(chain[i++], "(");
            AssertWhitespace(chain[i++], " ");
            AssertText(chain[i++], "(");
            AssertIdentifier(chain[i++], "E");
            AssertText(chain[i++], ")");
            AssertText(chain[i++], ")");
            AssertWhitespace(chain[i++], " ");
            AssertText(chain[i++], ")");
        }

        [Fact]
        public void ParseInvocationWithNestedParenthesisExpansion()
        {
            var tokens = PreprocessorExpressionLexer.Lex("F1((F2() (B)), F3((C (F4()))))").ToArray();
            var result = PreprocessorExpressionParser.ParseExpansion(tokens);
            var f1Arguments = AssertInvocation(result, "F1");
            Assert.Equal(2, f1Arguments.Length);
            var firstChain = AssertChain(f1Arguments[0], 7);
            var i = 0;
            AssertText(firstChain[i++], "(");
            var f2Arguments = AssertInvocation(firstChain[i++], "F2");
            Assert.Empty(f2Arguments);
            AssertWhitespace(firstChain[i++], " ");
            AssertText(firstChain[i++], "(");
            AssertIdentifier(firstChain[i++], "B");
            AssertText(firstChain[i++], ")");
            var secondChain = AssertChain(f1Arguments[1], 1);
            var thirdChain = AssertChain(secondChain[0], 8);
            i = 0;
            AssertWhitespace(thirdChain[i++], " ");
            var f3Arguments = AssertInvocation(thirdChain[i++], "F3");
            Assert.Single(f3Arguments);
            var fourthChain = AssertChain(f3Arguments[0], 8);
            i = 0;
            AssertText(fourthChain[i++], "(");
            AssertIdentifier(fourthChain[i++], "C");
            AssertWhitespace(fourthChain[i++], " ");
            AssertText(fourthChain[i++], "(");
            var subnestedArguments = AssertInvocation(fourthChain[i++], "F4");
            Assert.Empty(subnestedArguments);
            AssertText(fourthChain[i++], ")");
            AssertText(fourthChain[i++], ")");
        }
    }
}
