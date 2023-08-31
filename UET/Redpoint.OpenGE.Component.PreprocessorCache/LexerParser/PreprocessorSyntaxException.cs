namespace Redpoint.OpenGE.Component.PreprocessorCache.LexerParser
{
    using Redpoint.OpenGE.Protocol;
    using System;

    internal class PreprocessorSyntaxException : Exception
    {
        public PreprocessorSyntaxException(PreprocessorExpressionToken[] tokens, int position)
            : base(position < tokens.Length ? $"Unexpected token at position {position}: {tokens[position]}" : "Unexpected end of token stream")
        {
        }
    }
}