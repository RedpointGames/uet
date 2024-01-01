namespace Redpoint.OpenGE.LexerParser
{
    using Redpoint.Lexer;

    ref struct DirectiveRange
    {
        public bool Found;
        public LexerRange Directive;
        public LexerRange Arguments;
    }
}
