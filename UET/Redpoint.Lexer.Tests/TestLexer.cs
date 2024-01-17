namespace Redpoint.Lexer.Tests
{
    public partial class TestLexer
    {
        [LexerTokenizer("test")]
        public static partial ReadOnlySpan<char> ConsumeTest(ref ReadOnlySpan<char> span, ref LexerCursor cursor);

        [PermitNewlineContinuations]
        [LexerTokenizer("test")]
        public static partial LexerFragment ConsumeTestWithNewlines(ref ReadOnlySpan<char> span, ref LexerCursor cursor);

        [LexerTokenizer("[a-zA-Z_][a-zA-Z0-9_]*")]
        public static partial ReadOnlySpan<char> ConsumeWord(ref ReadOnlySpan<char> span, ref LexerCursor cursor);

        [PermitNewlineContinuations]
        [LexerTokenizer("[a-zA-Z_][a-zA-Z0-9_]*")]
        public static partial LexerFragment ConsumeWordWithNewlines(ref ReadOnlySpan<char> span, ref LexerCursor cursor);

        [LexerTokenizer("hello[0-9]wo[0-9]+rld[0-9]*done")]
        public static partial ReadOnlySpan<char> ConsumeAdvanced(ref ReadOnlySpan<char> span, ref LexerCursor cursor);
    }
}
