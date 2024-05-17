namespace Redpoint.CppPreprocessor.Lexing
{
    using Redpoint.Lexer;

    /// <summary>
    /// Represents the range of a directive within a source span.
    /// </summary>
    public ref struct DirectiveRange
    {
        /// <summary>
        /// If true, a directive was found.
        /// </summary>
        public bool Found;

        /// <summary>
        /// The range of the directive name, such as 'define'.
        /// </summary>
        public LexerRange Directive;

        /// <summary>
        /// If true, the directive name has newline continuations and can't be directly compared. Instead, you'll need to use a lexer tokenizer to detect if the directive is the type of directive you want.
        /// </summary>
        public bool DirectiveHasNewlineContinuations;

        /// <summary>
        /// The range of the directive's arguments, which will often contain newlines and newline continuations.
        /// </summary>
        public LexerRange Arguments;
    }
}
