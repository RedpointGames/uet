namespace Redpoint.CloudFramework.Repository.Converters.Expression
{
    using Google.Cloud.Datastore.V1;
    using Google.Type;
    using Redpoint.CloudFramework.Models;
    using System;

    internal class GeoQueryParameters<T> where T : Model
    {
        public required string GeoFieldName { get; set; }
        public required LatLng MinPoint { get; set; }
        public required LatLng MaxPoint { get; set; }
        public required LatLng CenterPoint { get; set; }
        public required Func<T, bool> ServerSideFilter { get; set; }
        public required Func<T, LatLng> ServerSideAccessor { get; set; }
        public required float DistanceKm { get; set; }
        public PropertyOrder.Types.Direction? SortDirection { get; set; }
    }
}
