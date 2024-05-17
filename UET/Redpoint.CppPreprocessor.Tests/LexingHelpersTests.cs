namespace Redpoint.CppPreprocessor.Tests
{
    using Redpoint.CppPreprocessor.Lexing;
    using Redpoint.Lexer;
    using System.Reflection;
    using System.Text;
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
        [InlineData(0, "/")]
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
        [InlineData("mult\\\niline\\\n0Identifier", "mult\\\niline\\\n0Identifier")]
        [InlineData("\\\nmultiline\\\n0Identifier", "\\\nmultiline\\\n0Identifier")]
        public void ConsumeWord(string expected, string test)
        {
            ReadOnlySpan<char> span = test.AsSpan();
            LexerCursor cursor = default;
            Assert.Equal(expected, LexingHelpers.ConsumeWord(ref span, ref cursor).Span.ToString());
        }

        public struct ExpectedDirective(string directive, string arguments = "")
        {
            public string Directive = directive;
            public string Arguments = arguments;
            public override string ToString()
            {
                return $"'{Directive}' '{Arguments}'";
            }
        }

        public static IEnumerable<object[]> GetNextDirectiveData()
        {
            yield return new object[]
            {
                """
                absolutely
                no direc\
                tives
                in th\r\nis file
                """.Replace("\r\n", "\n"),
                new ExpectedDirective[0]
            };
            yield return new object[]
            {
                """
                #define TEST A
                """.Replace("\r\n", "\n"),
                new[]
                {
                    new ExpectedDirective("define", "TEST A"),
                }
            };
            yield return new object[]
            {
                """
                #define TEST A \
                    EVEN MORE CONTENT
                """.Replace("\r\n", "\n"),
                new[]
                {
                    new ExpectedDirective("define", "TEST A \\\n    EVEN MORE CONTENT"),
                }
            };
            yield return new object[]
            {
                """
                /*
                */ # /*
                */ defi\
                ne FO\
                O 10\
                20
                """.Replace("\r\n", "\n"),
                new[]
                {
                    new ExpectedDirective("defi\\\nne", "FO\\\nO 10\\\n20"),
                }
            };
            yield return new object[]
            {
                """
                /*
                */ # /*
                */ defi\
                ne FO\
                O 10\
                20
                IGNORE THIS LINE
                /*
                */ # /*
                */ defi\
                ne FO\
                O 10\
                20
                """.Replace("\r\n", "\n"),
                new[]
                {
                    new ExpectedDirective("defi\\\nne", "FO\\\nO 10\\\n20"),
                    new ExpectedDirective("defi\\\nne", "FO\\\nO 10\\\n20"),
                }
            };
            using var reader = new StreamReader(
                Assembly.GetExecutingAssembly()
                    .GetManifestResourceStream("Redpoint.CppPreprocessor.Tests.assert.h")!);
            yield return new object[]
            {
                reader.ReadToEnd().Replace("\r\n", "\n").Replace("\n", "\r\n"),
                new[]
                {
                    new ExpectedDirective("if", "defined _VCRT_BUILD && !defined _ASSERT_OK"),
                    new ExpectedDirective("error", "assert.h not for CRT internal use"),
                    new ExpectedDirective("endif", ""),
                    new ExpectedDirective("include", "<corecrt.h>"),
                    new ExpectedDirective("undef", "assert"),
                    new ExpectedDirective("ifdef", "NDEBUG"),
                    new ExpectedDirective("define", "assert(expression) ((void)0)"),
                    new ExpectedDirective("else", ""),
                    new ExpectedDirective(
                        "define",
                        """
                        assert(expression) (void)(                                                       \
                                    (!!(expression)) ||                                                              \
                                    (_wassert(_CRT_WIDE(#expression), _CRT_WIDE(__FILE__), (unsigned)(__LINE__)), 0) \
                                )
                        """.Replace("\r\n", "\n").Replace("\n", "\r\n")),
                    new ExpectedDirective("endif", ""),
                }
            };
        }

        [Theory]
        [MemberData(nameof(GetNextDirectiveData))]
        public void GetNextDirective(string content, ExpectedDirective[] expectedDirectives)
        {
            var original = content.AsSpan();
            var range = original;
            LexerCursor cursor = default;
            for (int i = 0; i <= expectedDirectives.Length; i++)
            {
                var result = LexingHelpers.GetNextDirective(
                    ref range,
                    in original,
                    ref cursor);
                var directive = result.Found ? original.Slice(result.Directive.Start, result.Directive.Length) : default;
                var arguments = result.Found && result.Arguments.Length > 0 ? original.Slice(result.Arguments.Start, result.Arguments.Length) : default;
                if (i == expectedDirectives.Length)
                {
                    Assert.False(result.Found, $"Unexpected directive found on iteration {i}: '{result.Directive}' '{result.Arguments}'");
                }
                else
                {
                    Assert.True(result.Found, $"Did not find expected directive on iteration {i}: '{result.Directive}' '{result.Arguments}'");
                    Assert.Equal(expectedDirectives[i].Directive, directive.ToString());
                    Assert.Equal(expectedDirectives[i].Arguments, arguments.ToString());
                }
            }
        }

        [Fact]
        public void GetNextDirectiveSubsequent()
        {
            var content =
                """
                /*
                */ # /*
                */ defi\
                ne FO\
                O 10\
                20
                IGNORE THIS LINE
                /*
                */ # /*
                */ deCi\
                ne BA\
                R 30\
                40
                """.Replace("\r\n", "\n");
            var original = content.AsSpan();
            var range = original;
            LexerCursor cursor = default;
            var result = LexingHelpers.GetNextDirective(
                ref range,
                in original,
                ref cursor);
            Assert.True(result.Found, $"Did not find expected directive on first iteration.");
            var directive = result.Found ? original.Slice(result.Directive.Start, result.Directive.Length) : default;
            var arguments = result.Found && result.Arguments.Length > 0 ? original.Slice(result.Arguments.Start, result.Arguments.Length) : default;
            Assert.Equal("defi\\\nne", directive.ToString());
            Assert.Equal("FO\\\nO 10\\\n20", arguments.ToString());
            result = LexingHelpers.GetNextDirective(
                ref range,
                in original,
                ref cursor);
            Assert.True(result.Found, $"Did not find expected directive on second iteration.");
            directive = result.Found ? original.Slice(result.Directive.Start, result.Directive.Length) : default;
            arguments = result.Found && result.Arguments.Length > 0 ? original.Slice(result.Arguments.Start, result.Arguments.Length) : default;
            Assert.Equal("deCi\\\nne", directive.ToString());
            Assert.Equal("BA\\\nR 30\\\n40", arguments.ToString());
            result = LexingHelpers.GetNextDirective(
                ref range,
                in original,
                ref cursor);
            Assert.False(result.Found, $"Found unexpected directive on third iteration.");
        }
    }
}
