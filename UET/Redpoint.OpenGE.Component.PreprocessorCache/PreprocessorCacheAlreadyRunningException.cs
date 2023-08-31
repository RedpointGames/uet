namespace Redpoint.OpenGE.Component.PreprocessorCache
{
    using System;

    internal class PreprocessorCacheAlreadyRunningException : Exception
    {
        public PreprocessorCacheAlreadyRunningException() : base("Another instance of the OpenGE preprocessor cache is running.")
        {
        }
    }
}