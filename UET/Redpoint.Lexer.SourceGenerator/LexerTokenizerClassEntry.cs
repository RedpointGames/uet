namespace Redpoint.Lexer.SourceGenerator
{
    internal struct LexerTokenizerClassEntry
    {
        public string Name;
        public bool IsStatic;

        public override string ToString()
        {
            return Name;
        }
    }
}
