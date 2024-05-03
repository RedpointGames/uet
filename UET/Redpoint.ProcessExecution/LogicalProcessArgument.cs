namespace Redpoint.ProcessExecution
{
    /// <summary>
    /// Represents a logical argument to a process, without having any original argument information attached.
    /// </summary>
    public record class LogicalProcessArgument
    {
        private readonly string _logicalValue;

        /// <summary>
        /// Construct a new <see cref="LogicalProcessArgument"/> from a logical value. This will be processed into an escaped value when the process executor runs the process.
        /// </summary>
        /// <param name="logicalValue">The logical value, such as 'C:\'.</param>
        public LogicalProcessArgument(string logicalValue)
        {
            _logicalValue = logicalValue;
        }

        /// <summary>
        /// The logical value, without unnecessary quotes.
        /// </summary>
        public string LogicalValue => _logicalValue;

        /// <summary>
        /// Returns the string representation of this process argument.
        /// </summary>
        /// <returns>The string representation of this process argument.</returns>
        public override string ToString()
        {
            return LogicalValue;
        }

        /// <summary>
        /// Implicitly create a <see cref="LogicalProcessArgument"/> from a string value.
        /// </summary>
        /// <param name="argument">The logical string value for the argument.</param>
        public static implicit operator LogicalProcessArgument(string argument) => new LogicalProcessArgument(argument);

        /// <summary>
        /// Create a <see cref="LogicalProcessArgument"/> from a string value.
        /// </summary>
        /// <param name="argument">The logical string value for the argument.</param>
        public static LogicalProcessArgument FromString(string argument)
        {
            return new LogicalProcessArgument(argument);
        }
    }
}
