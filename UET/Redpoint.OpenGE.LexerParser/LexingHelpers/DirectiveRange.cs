namespace Redpoint.OpenGE.LexerParser
{
    using Redpoint.Lexer;

    internal ref struct DirectiveRange
    {
        public bool Found;
        public LexerRange Directive;
        public bool DirectiveHasNewlineContinuations;
        public LexerRange Arguments;
    }
}
