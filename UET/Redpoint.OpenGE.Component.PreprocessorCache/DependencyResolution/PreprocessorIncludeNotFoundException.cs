namespace Redpoint.OpenGE.Component.PreprocessorCache.DependencyResolution
{
    using System;

    public class PreprocessorIncludeNotFoundException : Exception
    {
        public PreprocessorIncludeNotFoundException(string searchValue) : base($"Preprocessor was unable to find any file that matched '{searchValue}'")
        {
            SearchValue = searchValue;
        }

        public string SearchValue { get; }
    }
}