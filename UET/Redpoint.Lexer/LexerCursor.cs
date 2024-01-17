namespace Redpoint.Lexer
{
    /// <summary>
    /// Represents the current position of the lexer within the
    /// original content.
    /// </summary>
    public ref struct LexerCursor
    {
        /// <summary>
        /// The number of characters, including whitespace and
        /// other control characters, consumed by the lexer so far.
        /// This value is such that you can use it as an input
        /// to <see cref="ReadOnlySpan{T}.Slice(int)"/> on
        /// the original content and get the current cursor 
        /// position of the lexer.
        /// </summary>
        public int CharactersConsumed;

        /// <summary>
        /// The number of newline sequences consumed, including
        /// via newline continuations. Thus this is effectively
        /// the "line number" that the lexer is up to, minus one
        /// (since this will report '0' for the first line in the
        /// content). The "{cr}{lf}" sequence is treated as a
        /// single newline.
        /// </summary>
        public int NewlinesConsumed;

        /// <summary>
        /// Add the values of another cursor to this one.
        /// </summary>
        /// <param name="other">The other cursor to add.</param>
        public void Add(ref readonly LexerCursor other)
        {
            CharactersConsumed += other.CharactersConsumed;
            NewlinesConsumed += other.NewlinesConsumed;
        }
    }
}
