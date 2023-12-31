namespace Redpoint.Lexer.SourceGenerator
{
    using Microsoft.CodeAnalysis;

    [Generator(LanguageNames.CSharp)]
    public class LexerSourceGenerator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new LexerTokenizerSyntaxReceiver());
        }

        public void Execute(GeneratorExecutionContext context)
        {
            if (context.SyntaxContextReceiver is ILexerSyntaxReceiver syntaxReceiver)
            {
                syntaxReceiver.Execute(context);
            }
        }
    }
}
