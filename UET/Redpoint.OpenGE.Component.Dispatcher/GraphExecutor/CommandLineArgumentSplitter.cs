namespace Redpoint.OpenGE.Component.Dispatcher.GraphExecutor
{
    using System.Collections.Generic;

    internal static class CommandLineArgumentSplitter
    {
        private static string RemoveUnescapedQuotes(string value)
        {
            var buffer = string.Empty;
            var potentialEscape = false;
            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] == '\\')
                {
                    buffer += value[i];
                    potentialEscape = true;
                }
                else if (value[i] == '"')
                {
                    if (potentialEscape)
                    {
                        buffer += value[i];
                    }
                    else
                    {
                        // Skip an unescaped quote, since we're splitting into arguments anyway.
                    }
                    potentialEscape = false;
                }
                else
                {
                    buffer += value[i];
                    potentialEscape = false;
                }
            }
            return buffer;
        }

        internal static string[] SplitArguments(string arguments)
        {
            var argumentList = new List<string>();
            var buffer = string.Empty;
            var inQuote = false;
            var isEscaping = false;
            for (int i = 0; i < arguments.Length; i++)
            {
                var chr = arguments[i];
                if (isEscaping)
                {
                    if (chr == '\\' || chr == '"')
                    {
                        buffer += chr;
                    }
                    else
                    {
                        buffer += '\\';
                        buffer += chr;
                    }
                    isEscaping = false;
                }
                else if (chr == '\\')
                {
                    isEscaping = true;
                }
                else if (chr == '"')
                {
                    // @todo: Do we need to handle \" sequence?
                    inQuote = !inQuote;
                }
                else if (inQuote)
                {
                    buffer += chr;
                }
                else if (chr == ' ')
                {
                    if (!string.IsNullOrWhiteSpace(buffer))
                    {
                        argumentList.Add(RemoveUnescapedQuotes(buffer));
                        buffer = string.Empty;
                    }
                }
                else
                {
                    buffer += chr;
                }
            }
            if (!string.IsNullOrWhiteSpace(buffer))
            {
                argumentList.Add(RemoveUnescapedQuotes(buffer));
            }
            return argumentList.ToArray();
        }
    }
}
