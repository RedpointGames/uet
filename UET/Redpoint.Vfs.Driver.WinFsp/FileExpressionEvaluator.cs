namespace Redpoint.Vfs.Driver.WinFsp
{
    /// <summary>
    /// Copied from DokanNet under the MIT license, since the implementation is the same.
    /// </summary>
    internal static class FileExpressionEvaluator
    {
        private const char _dosStar = '<';
        private const char _dosQm = '>';
        private const char _dosDot = '"';
        private const char _asterisk = '*';
        private const char _questionMark = '?';

        private readonly static char[] _charsThatMatchEmptyStringsAtEnd = { _dosDot, _dosStar, _asterisk };

        public static bool IsNameInExpression(string expression, string name, bool ignoreCase)
        {
            var ei = 0;
            var ni = 0;

            while (ei < expression.Length && ni < name.Length)
            {
                switch (expression[ei])
                {
                    case _asterisk:
                        ei++;
                        if (ei > expression.Length)
                            return true;

                        while (ni < name.Length)
                        {
                            if (IsNameInExpression(expression.Substring(ei), name.Substring(ni), ignoreCase))
                                return true;
                            ni++;
                        }

                        break;
                    case _dosStar:
                        var lastDotIndex = name.LastIndexOf('.');
                        ei++;

                        var endReached = false;
                        while (!endReached)
                        {
                            endReached = (ni >= name.Length || lastDotIndex > -1 && ni > lastDotIndex);

                            if (!endReached)
                            {
                                if (IsNameInExpression(expression.Substring(ei), name.Substring(ni), ignoreCase))
                                    return true;
                                ni++;
                            }
                        }

                        break;
                    case _dosQm:
                        ei++;
                        if (name[ni] != '.')
                        {
                            ni++;
                        }
                        else
                        {
                            var p = ni + 1;
                            while (p < name.Length)
                            {
                                if (name[p] == '.')
                                    break;
                                p++;
                            }

                            if (p < name.Length && name[p] == '.')
                                ni++;
                        }

                        break;
                    case _dosDot:
                        if (ei < expression.Length)
                        {
                            if (name[ni] != '.')
                                return false;
                            else
                                ni++;
                        }
                        else
                        {
                            if (name[ni] == '.')
                                ni++;
                        }
                        ei++;
                        break;
                    case _questionMark:
                        ei++;
                        ni++;
                        break;
                    default:
                        if (ignoreCase && char.ToUpperInvariant(expression[ei]) == char.ToUpperInvariant(name[ni]))
                        {
                            ei++;
                            ni++;
                        }
                        else if (!ignoreCase && expression[ei] == name[ni])
                        {
                            ei++;
                            ni++;
                        }
                        else
                        {
                            return false;
                        }

                        break;
                }
            }

            var nextExpressionChars = expression.Substring(ei);
            var areNextExpressionCharsAllNullMatchers = expression.Any() && !string.IsNullOrEmpty(nextExpressionChars) && nextExpressionChars.All(x => _charsThatMatchEmptyStringsAtEnd.Contains(x));
            var isNameCurrentCharTheLast = ni == name.Length;
            if (ei == expression.Length && isNameCurrentCharTheLast || isNameCurrentCharTheLast && areNextExpressionCharsAllNullMatchers)
                return true;

            return false;
        }
    }
}