namespace Redpoint.OpenGE.Component.PreprocessorCache.LexerParser
{
    using System;
    using System.Diagnostics.CodeAnalysis;

    public class PreprocessorScannerException : Exception
    {
        [SuppressMessage("Design", "CA1062:Validate arguments of public methods", Justification = "No reasonable way to validate this as it is passed to base().")]
        public PreprocessorScannerException(int lineNumber, string line, Exception innerException) :
            base($"Failed to parse line {lineNumber} '{line}': {innerException.Message}", innerException)
        {
        }
    }
}