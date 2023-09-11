namespace Redpoint.Uet.Configuration
{
    using System.Diagnostics.CodeAnalysis;
    using System.Text.Json;

    public class IncludedJsonException : JsonException
    {
        [SuppressMessage("Design", "CA1062:Validate arguments of public methods", Justification = "This argument is passed to base() so there is no practical way to validate it.")]
        public IncludedJsonException(JsonException ex, string path) : base(ex.Message, ex.Path, ex.LineNumber, ex.BytePositionInLine)
        {
            FilePath = path;
        }

        public string FilePath { get; }
    }
}
