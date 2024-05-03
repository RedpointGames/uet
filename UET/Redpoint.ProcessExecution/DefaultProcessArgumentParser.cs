namespace Redpoint.ProcessExecution
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    internal class DefaultProcessArgumentParser : IProcessArgumentParser
    {
        public EscapedProcessArgument CreateArgumentFromLogicalValue(LogicalProcessArgument logicalValue)
        {
            if (logicalValue is EscapedProcessArgument escapedValue)
            {
                return escapedValue;
            }
            else
            {
                return CreateArgumentFromLogicalValue(logicalValue.LogicalValue);
            }
        }

        public EscapedProcessArgument CreateArgumentFromLogicalValue(string logicalValue)
        {
            var inQuote = false;
            var isEscaping = false;
            var requiresQuoting = false;
            for (int i = 0; i < logicalValue.Length; i++)
            {
                var logicalChar = logicalValue[i];
                if (isEscaping)
                {
                    isEscaping = false;
                    continue;
                }
                else if (inQuote && logicalChar == '\\')
                {
                    isEscaping = true;
                }
                else if (logicalChar == '"')
                {
                    inQuote = !inQuote;
                }
                else if (logicalChar == ' ' || logicalChar == '\t' || logicalChar == '\n' || logicalChar == '\r')
                {
                    if (!inQuote)
                    {
                        requiresQuoting = true;
                        break;
                    }
                }
            }
            if (!requiresQuoting)
            {
                return new EscapedProcessArgument(logicalValue, logicalValue);
            }

            var originalValueBuilder = new StringBuilder();
            originalValueBuilder.Append('"');
            for (int i = 0; i < logicalValue.Length; i++)
            {
                var logicalChar = logicalValue[i];
                if (logicalChar == '"')
                {
                    originalValueBuilder.Append("\\\"");
                }
                else
                {
                    originalValueBuilder.Append(logicalChar);
                }
            }
            originalValueBuilder.Append('"');
            return new EscapedProcessArgument(logicalValue, originalValueBuilder.ToString());
        }

        public EscapedProcessArgument CreateArgumentFromOriginalValue(string originalValue)
        {
            var logicalValueBuilder = new StringBuilder();

            var inQuote = false;
            var isEscaping = false;
            for (int i = 0; i < originalValue.Length; i++)
            {
                var chr = originalValue[i];
                if (isEscaping)
                {
                    if (chr == '"' &&
                        (i == originalValue.Length - 1 ||
                         originalValue[i + 1] == ' '))
                    {
                        // @hack: Handle '"Path\"' as 'Path\' and not 'Path\"'.
                        logicalValueBuilder.Append('\\');
                        inQuote = false;
                    }
                    else
                    {
                        logicalValueBuilder.Append('\\');
                        logicalValueBuilder.Append(chr);
                    }
                    isEscaping = false;
                }
                else if (chr == '\\')
                {
                    isEscaping = true;
                }
                else if (chr == '"')
                {
                    inQuote = !inQuote;
                }
                else if (inQuote)
                {
                    logicalValueBuilder.Append(chr);
                }
                else if (chr == ' ')
                {
                    throw new InvalidOperationException($"Unescaped space in original value when calling CreateArgumentFromOriginalValue: {originalValue}");
                }
                else
                {
                    logicalValueBuilder.Append(chr);
                }
            }

            return new EscapedProcessArgument(logicalValueBuilder.ToString(), originalValue);
        }

        public IReadOnlyList<EscapedProcessArgument> SplitArguments(string arguments)
        {
            var rawArgumentList = new List<string>();

            var startIndex = 0;
            var inQuote = false;
            var isEscaping = false;
            for (int i = 0; i < arguments.Length; i++)
            {
                var chr = arguments[i];
                if (isEscaping)
                {
                    isEscaping = false;
                }
                else if (chr == '\\')
                {
                    isEscaping = true;
                }
                else if (chr == '"')
                {
                    inQuote = !inQuote;
                }
                else if (inQuote)
                {
                }
                else if (chr == ' ')
                {
                    if ((i - startIndex) > 0)
                    {
                        rawArgumentList.Add(arguments.Substring(startIndex, i - startIndex));
                    }
                    startIndex = i + 1;
                }
                else
                {
                }
            }
            if (startIndex != arguments.Length)
            {
                rawArgumentList.Add(arguments.Substring(startIndex, arguments.Length - startIndex));
            }

            var escapedArgumentList = rawArgumentList
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(CreateArgumentFromOriginalValue)
                .ToList();

            return escapedArgumentList;
        }

        public string JoinArguments(IEnumerable<LogicalProcessArgument> arguments)
        {
            var commandLine = new StringBuilder();
            foreach (var argument in arguments)
            {
                EscapedProcessArgument escapedProcessArgument;
                if (argument is EscapedProcessArgument escaped)
                {
                    escapedProcessArgument = escaped;
                }
                else
                {
                    escapedProcessArgument = CreateArgumentFromLogicalValue(argument);
                }
                if (commandLine.Length != 0)
                {
                    commandLine.Append(' ');
                }
                commandLine.Append(escapedProcessArgument.OriginalValue);
            }
            return commandLine.ToString();
        }
    }
}
