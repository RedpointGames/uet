namespace Redpoint.Lexer
{
    /// <summary>
    /// Represents a <see cref="ReadOnlySpan{T}"/> of UTF-16 characters
    /// that might contain newline continuations (and thus be unsafe
    /// for direct comparison). Methods which take a struct of this
    /// type can perform faster comparisons when the span is known
    /// not to contain newline continuations.
    /// </summary>
    public ref struct LexerFragmentUtf16
    {
        /// <summary>
        /// The fragment, which will contain newline continuations if
        /// <see cref="ContainsNewlineContinuations"/> is true.
        /// </summary>
        public required ReadOnlySpan<char> Span;

        /// <summary>
        /// If true, there are \{lf} or \{cr}{lf} sequences in the
        /// <see cref="Span"/> value, so it can't be fast compared
        /// with other values.
        /// </summary>
        public required bool ContainsNewlineContinuations;
    }
}
