namespace Redpoint.Logging.SingleLine
{
    using Microsoft.Extensions.Logging.Console;

    /// <summary>
    /// Additional options for the single line console formatter, on top of the options that <see cref="SimpleConsoleFormatterOptions"/> provides.
    /// </summary>
    public class ExtendedSimpleConsoleFormatterOptions : SimpleConsoleFormatterOptions
    {
        /// <summary>
        /// If true, both the timestamp and the log level will be omitted from messages.
        /// </summary>
        public bool OmitLogPrefix
        {
            get; set;
        }
    }
}
