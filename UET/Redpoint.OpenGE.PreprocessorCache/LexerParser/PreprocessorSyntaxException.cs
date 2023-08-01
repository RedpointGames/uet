﻿namespace Redpoint.OpenGE.PreprocessorCache.LexerParser
{
    using PreprocessorCacheApi;
    using System;

    internal class PreprocessorSyntaxException : Exception
    {
        public PreprocessorSyntaxException(PreprocessorExpressionToken[] tokens, int position)
            : base(position < tokens.Length ? $"Unexpected token at position {position}: {tokens[position]}" : "Unexpected end of token stream")
        {
        }
    }
}