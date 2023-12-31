namespace Redpoint.Lexer.SourceGenerator
{
    using Microsoft.CodeAnalysis;

    internal interface ILexerSyntaxReceiver
    {
        void Execute(GeneratorExecutionContext context);
    }
}
