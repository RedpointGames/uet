namespace Redpoint.CloudFramework.Repository.Pagination
{
    using Google.Protobuf;
    using Microsoft.AspNetCore.Mvc;

    /// <summary>
    /// Represents a cursor for paginated queries.
    /// </summary>
    [ModelBinder(typeof(PaginatedQueryCursorModelBinder))]
    [Newtonsoft.Json.JsonConverter(typeof(PaginatedQueryCursorNewtonConverter))]
    [System.Text.Json.Serialization.JsonConverter(typeof(PaginatedQueryCursorSystemConverter))]
    public class PaginatedQueryCursor
    {
        private readonly string? _cursor;

        public static readonly PaginatedQueryCursor Empty = new((string?)null);

        public PaginatedQueryCursor(ByteString bs)
        {
            if (bs == ByteString.Empty)
            {
                _cursor = null;
            }
            else
            {
                _cursor = bs?.ToBase64();
            }
        }

        public PaginatedQueryCursor(string? s)
        {
            if (string.IsNullOrWhiteSpace(s))
            {
                _cursor = null;
            }
            else
            {
                _cursor = s;
            }
        }

        public override string ToString()
        {
            return _cursor ?? string.Empty;
        }

        public override bool Equals(object? obj)
        {
            return obj is PaginatedQueryCursor && ((PaginatedQueryCursor)obj)._cursor == _cursor;
        }

        public override int GetHashCode()
        {
            return _cursor?.GetHashCode(StringComparison.Ordinal) ?? 0;
        }

        public static implicit operator string?(PaginatedQueryCursor qc) => qc == null ? null : qc._cursor;
        public static implicit operator ByteString(PaginatedQueryCursor qc) => qc?._cursor == null ? ByteString.Empty : ByteString.FromBase64(qc._cursor);
        public ByteString ToByteString() => (ByteString)this;
    }
}
