namespace Redpoint.ProcessExecution
{
    /// <summary>
    /// Represents an argument to a process, both the sanitized value (with quotes stripped) for logical processing in C#, and the original value that potentially has quotes that need to be preserved when passed into CreateProcess on Windows.
    /// </summary>
    public record class EscapedProcessArgument : LogicalProcessArgument
    {
        private readonly string _originalValue;

        /// <summary>
        /// Construct a new <see cref="EscapedProcessArgument"/> from a logical and original value. If you wish to create a <see cref="EscapedProcessArgument"/> from a single string value, use <see cref="IProcessArgumentParser.CreateArgumentFromLogicalValue(string)"/>, which will determine if the value needs to be quoted and return the appropriate <see cref="EscapedProcessArgument"/> instance.
        /// </summary>
        /// <param name="logicalValue">The logical value, such as 'C:\'.</param>
        /// <param name="originalValue">The original value which might be quoted, such as '"C:\"'.</param>
        public EscapedProcessArgument(string logicalValue, string originalValue) : base(logicalValue)
        {
            _originalValue = originalValue;
        }

        /// <summary>
        /// The original value, preserving quotes.
        /// </summary>
        public string OriginalValue => _originalValue;

        /// <summary>
        /// Returns the string representation of this process argument.
        /// </summary>
        /// <returns>The string representation of this process argument.</returns>
        public override string ToString()
        {
            return OriginalValue;
        }
    }
}
