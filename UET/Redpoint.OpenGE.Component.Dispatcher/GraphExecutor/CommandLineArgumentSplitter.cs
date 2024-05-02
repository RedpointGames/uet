namespace Redpoint.OpenGE.Component.Dispatcher.GraphExecutor
{
    using System.Collections.Generic;

    internal static class CommandLineArgumentSplitter
    {
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
                        argumentList.Add(buffer);
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
                argumentList.Add(buffer);
            }
            return argumentList.ToArray();
        }
    }
}
