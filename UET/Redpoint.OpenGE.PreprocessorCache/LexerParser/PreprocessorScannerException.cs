namespace Redpoint.OpenGE.PreprocessorCache.LexerParser
{
    using System;

    internal class PreprocessorScannerException : Exception
    {
        public PreprocessorScannerException(string line, PreprocessorSyntaxException innerException) : 
            base($"Failed to parse line '{line}': {innerException.Message}", innerException)
        {
        }
    }
}