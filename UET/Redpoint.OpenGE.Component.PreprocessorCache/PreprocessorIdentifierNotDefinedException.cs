namespace Redpoint.OpenGE.Component.PreprocessorCache
{
    using System;

    [Serializable]
    public class PreprocessorIdentifierNotDefinedException : Exception
    {
        public PreprocessorIdentifierNotDefinedException()
        {
        }

        public PreprocessorIdentifierNotDefinedException(string? message) : base(message)
        {
        }

        public PreprocessorIdentifierNotDefinedException(string? message, Exception? innerException) : base(message, innerException)
        {
        }
    }
}