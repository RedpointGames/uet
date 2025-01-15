namespace Redpoint.CloudFramework.Startup
{
    using System;

    public class DevelopmentStartupException : Exception
    {
        /// <inheritdoc />
        public DevelopmentStartupException(string message) : base(message) { }
    }
}
