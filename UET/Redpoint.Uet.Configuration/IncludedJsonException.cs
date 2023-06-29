namespace Redpoint.Uet.Configuration
{
    using System.Text.Json;

    public class IncludedJsonException : JsonException
    {
        public IncludedJsonException(JsonException ex, string path) : base(ex.Message, ex.Path, ex.LineNumber, ex.BytePositionInLine)
        {
            FilePath = path;
        }

        public string FilePath { get; }
    }
}
