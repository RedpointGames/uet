namespace Redpoint.Lexer
{
    /// <summary>
    /// A faster version of <see cref="Range"/> that represents the
    /// offset of a child span within parent content.
    /// </summary>
    public struct LexerRange
    {
        /// <summary>
        /// The start of the range in the parent content.
        /// </summary>
        public required int Start;

        /// <summary>
        /// The length of the range in the parent content.
        /// </summary>
        public required int Length;
    }
}
