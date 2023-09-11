namespace Redpoint.OpenGE.Component.PreprocessorCache.LexerParser
{
    using Redpoint.OpenGE.Protocol;
    using System;
    using System.Diagnostics.CodeAnalysis;

    public class PreprocessorSyntaxException : Exception
    {
        [SuppressMessage("Design", "CA1062:Validate arguments of public methods", Justification = "No reasonable way to include this logic as it part of the base() call.")]
        public PreprocessorSyntaxException(PreprocessorExpressionToken[] tokens, int position)
            : base(position < tokens.Length ? $"Unexpected token at position {position}: {tokens[position]}" : "Unexpected end of token stream")
        {
        }
    }
}