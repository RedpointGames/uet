namespace Redpoint.CppPreprocessor.Lexing
{
    using Redpoint.Lexer;
    using System;

    /// <summary>
    /// Provides helper functions for lexing preprocessor directives out of spans without allocating memory.
    /// </summary>
    public static partial class LexingHelpers
    {
        [PermitNewlineContinuations]
        [LexerTokenizer("[a-zA-Z_][a-zA-Z0-9_]*")]
        public static partial LexerFragmentUtf16 ConsumeWord(
            ref ReadOnlySpan<char> span,
            ref LexerCursor cursor);
    }
}
