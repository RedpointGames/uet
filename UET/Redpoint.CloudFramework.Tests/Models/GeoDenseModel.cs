using Google.Type;
using NodaTime;
using Redpoint.CloudFramework.Models;

namespace Redpoint.CloudFramework.Tests.Models
{
    [Kind("cf_geoDenseModel")]
    public sealed class GeoDenseModel : Model<GeoDenseModel>
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
