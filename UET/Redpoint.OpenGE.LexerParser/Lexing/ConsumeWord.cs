namespace Redpoint.OpenGE.LexerParser
{
    using Redpoint.Lexer;
    using System;

    internal static partial class LexingHelpers
    {
        [PermitNewlineContinuations]
        [LexerTokenizer("[a-zA-Z_][a-zA-Z0-9_]*")]
        public static partial LexerFragment ConsumeWord(
            ref ReadOnlySpan<char> span,
            ref LexerCursor cursor);
    }
}
