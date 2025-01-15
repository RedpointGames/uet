namespace Redpoint.CloudFramework.Repository.Pagination
{
    using Redpoint.CloudFramework.Models;
    using System.Collections.Generic;

    public record struct PaginatedQueryResult<T> where T : class, IModel, new()
    {
        public PaginatedQueryCursor? NextCursor { get; set; }

        public required IReadOnlyList<T> Results { get; set; }
    }
}
