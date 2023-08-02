namespace Redpoint.OpenGE.Component.PreprocessorCache
{
    using System;
    using System.Runtime.Serialization;

    [Serializable]
    internal class PreprocessorIdentifierNotDefinedException : Exception
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

        protected PreprocessorIdentifierNotDefinedException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}