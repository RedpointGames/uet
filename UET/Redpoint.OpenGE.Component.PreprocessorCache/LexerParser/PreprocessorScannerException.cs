namespace Redpoint.OpenGE.Component.PreprocessorCache.LexerParser
{
    using System;

    internal class PreprocessorScannerException : Exception
    {
        public PreprocessorScannerException(int lineNumber, string line, Exception innerException) :
            base($"Failed to parse line {lineNumber} '{line}': {innerException.Message}", innerException)
        {
        }
    }
}