namespace Redpoint.Lexer.SourceGenerator
{
    internal struct LexerTokenizerGenerationSpec
    {
        public string TokenizerPattern;
        public bool PermitNewlineContinuations;
        public string MethodName;
        public string ContainingNamespaceName;
        public string[] ContainingClassNames;
        public string DeclaringFilename;
        internal string AccessibilityModifiers;
    }
}
