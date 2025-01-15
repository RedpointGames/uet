using Google.Type;
using NodaTime;
using Redpoint.CloudFramework.Models;

namespace Redpoint.CloudFramework.Tests
{
    [Kind<GeoDenseModel>("cf_geoDenseModel")]
    public class GeoDenseModel : AttributedModel
    {
        [Type(FieldType.String), Indexed]
        public string? forTest { get; set; }

        [Type(FieldType.Timestamp), Indexed]
        public Instant? timestamp { get; set; }

        [Type(FieldType.String), Indexed]
        public string? descriptor { get; set; }

        [Type(FieldType.Geopoint), Geopoint(6), Indexed]
        public LatLng? location { get; set; }
    }
}
