namespace Redpoint.OpenGE.Component.PreprocessorCache
{
    using System;

    internal class PreprocessorResolutionException : Exception
    {
        private string[] _sourceFiles;

        public PreprocessorResolutionException(string sourceFile, Exception? innerException)
            : this(
                  innerException is PreprocessorResolutionException a
                    ? new[] { sourceFile }.Concat(a._sourceFiles).ToArray()
                    : new[] { sourceFile },
                  innerException is PreprocessorResolutionException b
                    ? b.InnerException
                    : innerException)
        {
        }

        private PreprocessorResolutionException(string[] sourceFile, Exception? trueInnerException)
            : base(
                $"Preprocessor failed while processing include stack: {string.Join("\n", sourceFile)}",
                trueInnerException)
        {
            _sourceFiles = sourceFile;
        }
    }
}