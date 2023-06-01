namespace Redpoint.UET.Core
{
    using Microsoft.Extensions.Logging.Console;

    internal class ExtendedSimpleConsoleFormatterOptions : SimpleConsoleFormatterOptions
    {
        public bool OmitLogPrefix
        {
            get; set;
        }
    }
}
