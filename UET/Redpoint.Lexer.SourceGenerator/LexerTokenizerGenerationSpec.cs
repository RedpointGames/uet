namespace Redpoint.Lexer.SourceGenerator
{
    internal struct LexerTokenizerGenerationSpec
    {
        public string TokenizerPattern;
        public bool PermitNewlineContinuations;
        public string MethodName;
        public string ContainingNamespaceName;
        public LexerTokenizerClassEntry[] ContainingClasses;
        public string DeclaringFilename;
        public string AccessibilityModifiers;
    }
}
