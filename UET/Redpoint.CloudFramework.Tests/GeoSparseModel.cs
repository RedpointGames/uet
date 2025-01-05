using Google.Type;
using NodaTime;
using Redpoint.CloudFramework.Models;

namespace Redpoint.CloudFramework.Tests
{
    [Kind<GeoSparseModel>("cf_geoSparseModel")]
    public class GeoSparseModel : AttributedModel
    {
        [Type(FieldType.String), Indexed]
        public string? forTest { get; set; }

        [Type(FieldType.Timestamp), Indexed]
        public Instant? timestamp { get; set; }

        [Type(FieldType.String), Indexed]
        public string? descriptor { get; set; }

        [Type(FieldType.Geopoint), Geopoint(1), Indexed]
        public LatLng? location { get; set; }
    }
}
