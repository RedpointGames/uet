namespace Redpoint.OpenGE.Component.PreprocessorCache
{
    using System;

    public class PreprocessorCacheAlreadyRunningException : Exception
    {
        public PreprocessorCacheAlreadyRunningException() : base("Another instance of the OpenGE preprocessor cache is running.")
        {
        }
    }
}