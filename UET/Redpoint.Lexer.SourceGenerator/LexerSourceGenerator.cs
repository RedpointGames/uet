namespace Redpoint.Lexer.SourceGenerator
{
    using Microsoft.CodeAnalysis;

    /// <summary>
    /// Generates code for the Redpoint.Lexer library.
    /// </summary>
    [Generator(LanguageNames.CSharp)]
    public class LexerSourceGenerator : ISourceGenerator
    {
        /// <summary>
        /// Initializes the source generator.
        /// </summary>
        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new LexerTokenizerSyntaxReceiver());
        }

        /// <summary>
        /// Executes the source generator.
        /// </summary>
        public void Execute(GeneratorExecutionContext context)
        {
            if (context.SyntaxContextReceiver is ILexerSyntaxReceiver syntaxReceiver)
            {
                syntaxReceiver.Execute(context);
            }
        }
    }
}
