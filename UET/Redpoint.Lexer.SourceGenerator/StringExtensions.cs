namespace Redpoint.Lexer.SourceGenerator
{
    using System.Text;

    internal static class StringExtensions
    {
        public static string WithIndent(this string str, int indent)
        {
            var indentStr = string.Empty.PadLeft(indent);
            var sb = new StringBuilder();
            var first = true;
            foreach (var line in str.Split('\n'))
            {
                sb.Append((first ? "" : "\n") + indentStr + line);
                first = false;
            }
            return sb.ToString();
        }
    }
}
