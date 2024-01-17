namespace Redpoint.Lexer
{
    /// <summary>
    /// Indicates that the source generator should implement the
    /// specified method as a tokenizer, which takes a reference
    /// to a <see cref="ReadOnlySpan{T}"/> pointing to the start of
    /// the token and returns the tokenized <see cref="ReadOnlySpan{T}"/>
    /// (or <see cref="LexerFragment"/> if <see cref="PermitNewlineContinuationsAttribute"/> 
    /// is also set).
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class LexerTokenizerAttribute : Attribute
    {
        /// <summary>
        /// Construct a new lexer tokenizer attribute, which specifies
        /// the pattern that this lexer method should implement.
        /// </summary>
        /// <param name="pattern">The regex-like lexer pattern to handle.</param>
        public LexerTokenizerAttribute(string pattern)
        {
            ArgumentNullException.ThrowIfNull(pattern);
            Pattern = pattern;
        }

        /// <summary>
        /// The regex-like lexer pattern that is handled by this method.
        /// </summary>
        public string Pattern { get; }
    }
}
