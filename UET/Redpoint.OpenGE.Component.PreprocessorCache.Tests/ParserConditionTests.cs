namespace Redpoint.OpenGE.Component.PreprocessorCache.Tests
{
    using Redpoint.OpenGE.Component.PreprocessorCache.LexerParser;
    using Redpoint.OpenGE.Protocol;

    public class ParserConditionTests
    {
        private static string RenderTokenType(PreprocessorExpressionTokenType tokenType)
        {
            return PreprocessorExpressionParser._literalMappings[tokenType];
        }

        private static string RenderExpression(PreprocessorExpression expression)
        {
            switch (expression.ExprCase)
            {
                case PreprocessorExpression.ExprOneofCase.Binary:
                    return $"({RenderExpression(expression.Binary.Left)}{RenderTokenType(expression.Binary.Type)}{RenderExpression(expression.Binary.Right)})";
                case PreprocessorExpression.ExprOneofCase.Unary:
                    return $"({RenderTokenType(expression.Unary.Type)}{RenderExpression(expression.Unary.Expression)})";
                case PreprocessorExpression.ExprOneofCase.Chain:
                    return string.Join(string.Empty, expression.Chain.Expressions.Select(RenderExpression));
                case PreprocessorExpression.ExprOneofCase.Whitespace:
                    return expression.Whitespace;
                case PreprocessorExpression.ExprOneofCase.Invoke:
                    return $"{expression.Invoke.Identifier}({string.Join(',', expression.Invoke.Arguments.Select(RenderExpression))})";
                case PreprocessorExpression.ExprOneofCase.Token:
                    switch (expression.Token.DataCase)
                    {
                        case PreprocessorExpressionToken.DataOneofCase.Type:
                            return RenderTokenType(expression.Token.Type);
                        case PreprocessorExpressionToken.DataOneofCase.Text:
                            return $"[{expression.Token.Text}]";
                        case PreprocessorExpressionToken.DataOneofCase.Identifier:
                            return $"[{expression.Token.Identifier}]";
                        case PreprocessorExpressionToken.DataOneofCase.Number:
                            return $"{expression.Token.Number}";
                        default:
                            // unknown
                            return "@";
                    }
                case PreprocessorExpression.ExprOneofCase.Defined:
                    return $"defined({expression.Defined})";
                case PreprocessorExpression.ExprOneofCase.HasInclude:
                    return $"__has_include({expression.HasInclude})";
                default:
                    // unknown
                    return "@";
            }
        }

        [Fact]
        public void SimpleExpression()
        {
            Assert.Equal("(4+5)", RenderExpression(
                PreprocessorExpressionParser.ParseCondition(
                    PreprocessorExpressionLexer.Lex("4+5"))));
        }

        [Fact]
        public void PrecedenceTests()
        {
            Assert.Equal("((4+5)+6)", RenderExpression(
                PreprocessorExpressionParser.ParseCondition(
                    PreprocessorExpressionLexer.Lex("4+5+6"))));
            Assert.Equal("((4*5)+6)", RenderExpression(
                PreprocessorExpressionParser.ParseCondition(
                    PreprocessorExpressionLexer.Lex("4*5+6"))));
            Assert.Equal("(4+(5*6))", RenderExpression(
                PreprocessorExpressionParser.ParseCondition(
                    PreprocessorExpressionLexer.Lex("4+5*6"))));
            Assert.Equal("(4+((5*6)+7))", RenderExpression(
                PreprocessorExpressionParser.ParseCondition(
                    PreprocessorExpressionLexer.Lex("4+5*6+7"))));
            Assert.Equal("((4*5)+(6*7))", RenderExpression(
                PreprocessorExpressionParser.ParseCondition(
                    PreprocessorExpressionLexer.Lex("4*5+6*7"))));
        }

        [Fact]
        public void WhitespaceTests()
        {
            Assert.Equal("((4+5)+6)", RenderExpression(
                PreprocessorExpressionParser.ParseCondition(
                    PreprocessorExpressionLexer.Lex("4 + 5 + 6"))));
            Assert.Equal("((4*5)+6)", RenderExpression(
                PreprocessorExpressionParser.ParseCondition(
                    PreprocessorExpressionLexer.Lex("4 * 5 +\t6"))));
            Assert.Equal("(4+(5*6))", RenderExpression(
                PreprocessorExpressionParser.ParseCondition(
                    PreprocessorExpressionLexer.Lex("4\t+ 5 *\t6"))));
            Assert.Equal("(4+((5*6)+7))", RenderExpression(
                PreprocessorExpressionParser.ParseCondition(
                    PreprocessorExpressionLexer.Lex("  4   + \t5\t\t*    6 +\t\t 7\t"))));
            Assert.Equal("((4*5)+(6*7))", RenderExpression(
                PreprocessorExpressionParser.ParseCondition(
                    PreprocessorExpressionLexer.Lex("\t4\t*\n5\n+\n6\n*\n7\n"))));
        }

        [Fact]
        public void InvocationTests()
        {
            Assert.Equal("(4+(CALL(5,6)*7))", RenderExpression(
                PreprocessorExpressionParser.ParseCondition(
                    PreprocessorExpressionLexer.Lex("4+CALL(5,6)*7"))));
        }

        [Fact]
        public void VariableTests()
        {
            Assert.Equal("(4+([VAR]*7))", RenderExpression(
                PreprocessorExpressionParser.ParseCondition(
                    PreprocessorExpressionLexer.Lex("4+VAR*7"))));
        }

        [Fact]
        public void UnaryTests()
        {
            Assert.Equal("(4+(0-7))", RenderExpression(
                PreprocessorExpressionParser.ParseCondition(
                    PreprocessorExpressionLexer.Lex("4+-7"))));
            Assert.Equal("(4+((0-7)*5))", RenderExpression(
                PreprocessorExpressionParser.ParseCondition(
                    PreprocessorExpressionLexer.Lex("4+-7*5"))));
        }

        [Fact]
        public void ComparisonTests()
        {
            Assert.Equal("(1==1)", RenderExpression(
                PreprocessorExpressionParser.ParseCondition(
                    PreprocessorExpressionLexer.Lex("1 == 1"))));
            Assert.Equal("((1==1)!=0)", RenderExpression(
                PreprocessorExpressionParser.ParseCondition(
                    PreprocessorExpressionLexer.Lex("1 == 1 != 0"))));
        }

        [Fact]
        public void DefinedTests()
        {
            Assert.Equal("defined(X)", RenderExpression(
                PreprocessorExpressionParser.ParseCondition(
                    PreprocessorExpressionLexer.Lex("defined(X)"))));
            Assert.Equal("(!defined(X))", RenderExpression(
                PreprocessorExpressionParser.ParseCondition(
                    PreprocessorExpressionLexer.Lex("!defined(X)"))));
            Assert.Equal("(defined(X)&&defined(Y))", RenderExpression(
                PreprocessorExpressionParser.ParseCondition(
                    PreprocessorExpressionLexer.Lex("defined(X) && defined(Y)"))));
            Assert.Equal("(defined(X)&&(!defined(Y)))", RenderExpression(
                PreprocessorExpressionParser.ParseCondition(
                    PreprocessorExpressionLexer.Lex("defined(X) && !defined(Y)"))));
            Assert.Equal("((!defined(X))&&defined(Y))", RenderExpression(
                PreprocessorExpressionParser.ParseCondition(
                    PreprocessorExpressionLexer.Lex("!defined(X) && defined(Y)"))));
            // MSVC nonsense.
            Assert.Equal("(defined(X)&&defined(Y))", RenderExpression(
                PreprocessorExpressionParser.ParseCondition(
                    PreprocessorExpressionLexer.Lex("defined X && defined Y"))));
            Assert.Equal("(defined(X)&&(!defined(Y)))", RenderExpression(
                PreprocessorExpressionParser.ParseCondition(
                    PreprocessorExpressionLexer.Lex("defined X && !defined Y"))));
        }

        [Fact]
        public void HasIncludeTests()
        {
            Assert.Equal("__has_include(<dir/file.h>)", RenderExpression(
                PreprocessorExpressionParser.ParseCondition(
                    PreprocessorExpressionLexer.Lex("__has_include(<dir/file.h>)"))));
            Assert.Equal("__has_include(\"dir/file.h\")", RenderExpression(
                PreprocessorExpressionParser.ParseCondition(
                    PreprocessorExpressionLexer.Lex("__has_include(\"dir/file.h\")"))));
        }
    }
}
