namespace Redpoint.OpenGE.Component.PreprocessorCache.Tests
{
    using Redpoint.OpenGE.Component.PreprocessorCache.LexerParser;
    using Redpoint.OpenGE.Protocol;

    public class ScannerTests
    {
        [SkippableFact]
        public void ScanPlatformHeader()
        {
            var path = @"C:\Work\UE5\Engine\Source\Runtime\Core\Public\HAL\Platform.h";
            Skip.IfNot(File.Exists(path), "Requires UE5 to be present on disk.");
            var lines = File.ReadAllLines(path);

            // @note: We just want to make sure this gets parsed correctly.
            _ = PreprocessorScanner.Scan(lines);
        }

        [Fact]
        public void ScanNestedIf()
        {
            var lines = new[]
            {
                "#ifndef ABC",
                "#if 1",
                "#include <system.h>",
                "#endif",
                "#endif",
            };
            // @note: This should work.
            _ = PreprocessorScanner.Scan(lines);
        }

        [Fact]
        public void ScanDefinesAndIncludes()
        {
            var lines = new[]
            {
                "#include \"include.h\"",
                "#include <system.h>",
                "#define PREPROCESSOR_TO_STRING(x) PREPROCESSOR_TO_STRING_INNER(x)",
                "#define PREPROCESSOR_TO_STRING_INNER(x) #x",
                "#define PREPROCESSOR_JOIN(x, y) PREPROCESSOR_JOIN_INNER(x, y)",
                "#define PREPROCESSOR_JOIN_INNER(x, y) x##y",
                "#define PLATFORM_HEADER_NAME Windows",
                "#define COMPILED_PLATFORM_HEADER(Suffix) PREPROCESSOR_TO_STRING(PREPROCESSOR_JOIN(PLATFORM_HEADER_NAME, Suffix))",
                "#include COMPILED_PLATFORM_HEADER(Test)",
            };

            var directives = PreprocessorScanner.Scan(lines).Directives;

            Assert.Equal(9, directives.Count);

            int i = 0;
            Assert.Equal(PreprocessorDirective.DirectiveOneofCase.Include, directives[i].DirectiveCase);
            Assert.Equal(PreprocessorDirectiveInclude.IncludeOneofCase.Normal, directives[i].Include.IncludeCase);
            Assert.Equal("include.h", directives[i].Include.Normal);

            i++;
            Assert.Equal(PreprocessorDirective.DirectiveOneofCase.Include, directives[i].DirectiveCase);
            Assert.Equal(PreprocessorDirectiveInclude.IncludeOneofCase.System, directives[i].Include.IncludeCase);
            Assert.Equal("system.h", directives[i].Include.System);

            i++;
            Assert.Equal(PreprocessorDirective.DirectiveOneofCase.Define, directives[i].DirectiveCase);
            Assert.Equal("PREPROCESSOR_TO_STRING", directives[i].Define.Identifier);
            Assert.True(directives[i].Define.IsFunction);
            Assert.Single(directives[i].Define.Parameters);
            Assert.Equal("x", directives[i].Define.Parameters[0]);
            Assert.Equal(PreprocessorExpression.ExprOneofCase.Invoke, directives[i].Define.Expansion.ExprCase);
            Assert.Equal("PREPROCESSOR_TO_STRING_INNER", directives[i].Define.Expansion.Invoke.Identifier);
            Assert.Single(directives[i].Define.Expansion.Invoke.Arguments);
            Assert.Equal(PreprocessorExpression.ExprOneofCase.Token, directives[i].Define.Expansion.Invoke.Arguments[0].ExprCase);
            Assert.Equal(PreprocessorExpressionToken.DataOneofCase.Identifier, directives[i].Define.Expansion.Invoke.Arguments[0].Token.DataCase);
            Assert.Equal("x", directives[i].Define.Expansion.Invoke.Arguments[0].Token.Identifier);

            i++;
            Assert.Equal(PreprocessorDirective.DirectiveOneofCase.Define, directives[i].DirectiveCase);
            Assert.Equal("PREPROCESSOR_TO_STRING_INNER", directives[i].Define.Identifier);
            Assert.True(directives[i].Define.IsFunction);
            Assert.Single(directives[i].Define.Parameters);
            Assert.Equal("x", directives[i].Define.Parameters[0]);
            Assert.Equal(PreprocessorExpression.ExprOneofCase.Unary, directives[i].Define.Expansion.ExprCase);
            Assert.Equal(PreprocessorExpressionTokenType.Stringify, directives[i].Define.Expansion.Unary.Type);
            Assert.Equal(PreprocessorExpression.ExprOneofCase.Token, directives[i].Define.Expansion.Unary.Expression.ExprCase);
            Assert.Equal(PreprocessorExpressionToken.DataOneofCase.Identifier, directives[i].Define.Expansion.Unary.Expression.Token.DataCase);
            Assert.Equal("x", directives[i].Define.Expansion.Unary.Expression.Token.Identifier);

            i++;
            Assert.Equal(PreprocessorDirective.DirectiveOneofCase.Define, directives[i].DirectiveCase);
            Assert.Equal("PREPROCESSOR_JOIN", directives[i].Define.Identifier);
            Assert.True(directives[i].Define.IsFunction);
            Assert.Equal(2, directives[i].Define.Parameters.Count);
            Assert.Equal("x", directives[i].Define.Parameters[0]);
            Assert.Equal("y", directives[i].Define.Parameters[1]);
            Assert.Equal(PreprocessorExpression.ExprOneofCase.Invoke, directives[i].Define.Expansion.ExprCase);
            Assert.Equal("PREPROCESSOR_JOIN_INNER", directives[i].Define.Expansion.Invoke.Identifier);
            Assert.Equal(2, directives[i].Define.Expansion.Invoke.Arguments.Count);
            Assert.Equal(PreprocessorExpression.ExprOneofCase.Token, directives[i].Define.Expansion.Invoke.Arguments[0].ExprCase);
            Assert.Equal(PreprocessorExpressionToken.DataOneofCase.Identifier, directives[i].Define.Expansion.Invoke.Arguments[0].Token.DataCase);
            Assert.Equal("x", directives[i].Define.Expansion.Invoke.Arguments[0].Token.Identifier);
            Assert.Equal(PreprocessorExpression.ExprOneofCase.Chain, directives[i].Define.Expansion.Invoke.Arguments[1].ExprCase);
            Assert.Equal(2, directives[i].Define.Expansion.Invoke.Arguments[1].Chain.Expressions.Count);
            Assert.Equal(PreprocessorExpression.ExprOneofCase.Whitespace, directives[i].Define.Expansion.Invoke.Arguments[1].Chain.Expressions[0].ExprCase);
            Assert.Equal(" ", directives[i].Define.Expansion.Invoke.Arguments[1].Chain.Expressions[0].Whitespace);
            Assert.Equal(PreprocessorExpression.ExprOneofCase.Token, directives[i].Define.Expansion.Invoke.Arguments[1].Chain.Expressions[1].ExprCase);
            Assert.Equal(PreprocessorExpressionToken.DataOneofCase.Identifier, directives[i].Define.Expansion.Invoke.Arguments[1].Chain.Expressions[1].Token.DataCase);
            Assert.Equal("y", directives[i].Define.Expansion.Invoke.Arguments[1].Chain.Expressions[1].Token.Identifier);

            i++;
            Assert.Equal(PreprocessorDirective.DirectiveOneofCase.Define, directives[i].DirectiveCase);
            Assert.Equal("PREPROCESSOR_JOIN_INNER", directives[i].Define.Identifier);
            Assert.True(directives[i].Define.IsFunction);
            Assert.Equal(2, directives[i].Define.Parameters.Count);
            Assert.Equal("x", directives[i].Define.Parameters[0]);
            Assert.Equal("y", directives[i].Define.Parameters[1]);
            Assert.Equal(PreprocessorExpression.ExprOneofCase.Chain, directives[i].Define.Expansion.ExprCase);
            Assert.Equal(3, directives[i].Define.Expansion.Chain.Expressions.Count);
            Assert.Equal(PreprocessorExpression.ExprOneofCase.Token, directives[i].Define.Expansion.Chain.Expressions[0].ExprCase);
            Assert.Equal(PreprocessorExpressionToken.DataOneofCase.Identifier, directives[i].Define.Expansion.Chain.Expressions[0].Token.DataCase);
            Assert.Equal("x", directives[i].Define.Expansion.Chain.Expressions[0].Token.Identifier);
            Assert.Equal(PreprocessorExpression.ExprOneofCase.Token, directives[i].Define.Expansion.Chain.Expressions[1].ExprCase);
            Assert.Equal(PreprocessorExpressionToken.DataOneofCase.Type, directives[i].Define.Expansion.Chain.Expressions[1].Token.DataCase);
            Assert.Equal(PreprocessorExpressionTokenType.Join, directives[i].Define.Expansion.Chain.Expressions[1].Token.Type);
            Assert.Equal(PreprocessorExpression.ExprOneofCase.Token, directives[i].Define.Expansion.Chain.Expressions[2].ExprCase);
            Assert.Equal(PreprocessorExpressionToken.DataOneofCase.Identifier, directives[i].Define.Expansion.Chain.Expressions[2].Token.DataCase);
            Assert.Equal("y", directives[i].Define.Expansion.Chain.Expressions[2].Token.Identifier);

            i++;
            Assert.Equal(PreprocessorDirective.DirectiveOneofCase.Define, directives[i].DirectiveCase);
            Assert.Equal("PLATFORM_HEADER_NAME", directives[i].Define.Identifier);
            Assert.False(directives[i].Define.IsFunction);
            Assert.Empty(directives[i].Define.Parameters);
            Assert.Equal(PreprocessorExpression.ExprOneofCase.Token, directives[i].Define.Expansion.ExprCase);
            Assert.Equal(PreprocessorExpressionToken.DataOneofCase.Identifier, directives[i].Define.Expansion.Token.DataCase);
            Assert.Equal("Windows", directives[i].Define.Expansion.Token.Identifier);

            i++;
            Assert.Equal(PreprocessorDirective.DirectiveOneofCase.Define, directives[i].DirectiveCase);
            Assert.Equal("COMPILED_PLATFORM_HEADER", directives[i].Define.Identifier);
            Assert.True(directives[i].Define.IsFunction);
            Assert.Single(directives[i].Define.Parameters);
            Assert.Equal("Suffix", directives[i].Define.Parameters[0]);
            Assert.Equal(PreprocessorExpression.ExprOneofCase.Invoke, directives[i].Define.Expansion.ExprCase);
            Assert.Equal("PREPROCESSOR_TO_STRING", directives[i].Define.Expansion.Invoke.Identifier);
            Assert.Single(directives[i].Define.Expansion.Invoke.Arguments);
            Assert.Equal(PreprocessorExpression.ExprOneofCase.Invoke, directives[i].Define.Expansion.Invoke.Arguments[0].ExprCase);
            Assert.Equal("PREPROCESSOR_JOIN", directives[i].Define.Expansion.Invoke.Arguments[0].Invoke.Identifier);
            Assert.Equal(2, directives[i].Define.Expansion.Invoke.Arguments[0].Invoke.Arguments.Count);
            Assert.Equal(PreprocessorExpression.ExprOneofCase.Token, directives[i].Define.Expansion.Invoke.Arguments[0].Invoke.Arguments[0].ExprCase);
            Assert.Equal(PreprocessorExpressionToken.DataOneofCase.Identifier, directives[i].Define.Expansion.Invoke.Arguments[0].Invoke.Arguments[0].Token.DataCase);
            Assert.Equal("PLATFORM_HEADER_NAME", directives[i].Define.Expansion.Invoke.Arguments[0].Invoke.Arguments[0].Token.Identifier);
            Assert.Equal(PreprocessorExpression.ExprOneofCase.Chain, directives[i].Define.Expansion.Invoke.Arguments[0].Invoke.Arguments[1].ExprCase);
            Assert.Equal(2, directives[i].Define.Expansion.Invoke.Arguments[0].Invoke.Arguments[1].Chain.Expressions.Count);
            Assert.Equal(PreprocessorExpression.ExprOneofCase.Whitespace, directives[i].Define.Expansion.Invoke.Arguments[0].Invoke.Arguments[1].Chain.Expressions[0].ExprCase);
            Assert.Equal(" ", directives[i].Define.Expansion.Invoke.Arguments[0].Invoke.Arguments[1].Chain.Expressions[0].Whitespace);
            Assert.Equal(PreprocessorExpression.ExprOneofCase.Token, directives[i].Define.Expansion.Invoke.Arguments[0].Invoke.Arguments[1].Chain.Expressions[1].ExprCase);
            Assert.Equal(PreprocessorExpressionToken.DataOneofCase.Identifier, directives[i].Define.Expansion.Invoke.Arguments[0].Invoke.Arguments[1].Chain.Expressions[1].Token.DataCase);
            Assert.Equal("Suffix", directives[i].Define.Expansion.Invoke.Arguments[0].Invoke.Arguments[1].Chain.Expressions[1].Token.Identifier);

            i++;
            Assert.Equal(PreprocessorDirective.DirectiveOneofCase.Include, directives[i].DirectiveCase);
            Assert.Equal(PreprocessorDirectiveInclude.IncludeOneofCase.Expansion, directives[i].Include.IncludeCase);
            Assert.Equal(PreprocessorExpression.ExprOneofCase.Invoke, directives[i].Include.Expansion.ExprCase);
            Assert.Equal("COMPILED_PLATFORM_HEADER", directives[i].Include.Expansion.Invoke.Identifier);
            Assert.Single(directives[i].Include.Expansion.Invoke.Arguments);
            Assert.Equal(PreprocessorExpression.ExprOneofCase.Token, directives[i].Include.Expansion.Invoke.Arguments[0].ExprCase);
            Assert.Equal(PreprocessorExpressionToken.DataOneofCase.Identifier, directives[i].Include.Expansion.Invoke.Arguments[0].Token.DataCase);
            Assert.Equal("Test", directives[i].Include.Expansion.Invoke.Arguments[0].Token.Identifier);
        }
    }
}
