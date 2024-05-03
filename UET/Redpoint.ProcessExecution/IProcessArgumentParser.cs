namespace Redpoint.ProcessExecution
{
    using System.Collections.Generic;

    /// <summary>
    /// Provides APIs for splitting and joining arguments between string and array-of-string formats.
    /// </summary>
    public interface IProcessArgumentParser
    {
        /// <summary>
        /// Returns a <see cref="EscapedProcessArgument"/> that can represent this logical value on the command-line, quoting the value if necessary.
        /// </summary>
        /// <param name="logicalValue">The logical value.</param>
        /// <returns>The new process argument.</returns>
        EscapedProcessArgument CreateArgumentFromLogicalValue(LogicalProcessArgument logicalValue);

        /// <summary>
        /// Returns a <see cref="EscapedProcessArgument"/> that can represent this logical value on the command-line, quoting the value if necessary.
        /// </summary>
        /// <param name="logicalValue">The logical value.</param>
        /// <returns>The new process argument.</returns>
        EscapedProcessArgument CreateArgumentFromLogicalValue(string logicalValue);

        /// <summary>
        /// Returns a <see cref="EscapedProcessArgument"/> that can represent this potentially quoted, original value on the command-line, determining the logical value.
        /// </summary>
        /// <param name="originalValue">The original argument value.</param>
        /// <returns>The new process argument.</returns>
        EscapedProcessArgument CreateArgumentFromOriginalValue(string originalValue);

        /// <summary>
        /// Parses an argument command line and splits it into an array of arguments.
        /// </summary>
        /// <param name="arguments">The original argument string.</param>
        /// <returns>A list of arguments.</returns>
        IReadOnlyList<EscapedProcessArgument> SplitArguments(string arguments);

        /// <summary>
        /// Joins the array of arguments into an argument command line.
        /// </summary>
        /// <param name="arguments">The array of arguments.</param>
        /// <returns>The argument string.</returns>
        string JoinArguments(IEnumerable<LogicalProcessArgument> arguments);
    }
}
