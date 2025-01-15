namespace Redpoint.CloudFramework.Datastore
{
    using System.Collections.Generic;
    using Google.Cloud.Datastore.V1;
    using Google.Protobuf;
    using Redpoint.CloudFramework.Models;

    public sealed class MappedDatastoreQueryResults<T> where T : class, IModel, new()
    {
        public required ByteString EndCursor { get; set; }
        public required string? EndCursorForClients { get; set; }
        public required QueryResultBatch.Types.MoreResultsType MoreResults { get; set; }
        public required ICollection<T> Entities { get; set; }
    }
}
