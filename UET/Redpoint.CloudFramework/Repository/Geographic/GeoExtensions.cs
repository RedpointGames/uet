namespace Redpoint.CloudFramework.Repository
{
    using Google.Type;
    using System;

    public static class GeoExtensions
    {
        private const double _kmToNmUnit = 1.0 / 1.852;
        private const double _nmToLatLngUnit = 1.0 / 60.0;
        private const double _kmToLatLngUnit = _kmToNmUnit * _nmToLatLngUnit;
        
        internal static double HaversineDistance(LatLng pos1, LatLng pos2)
        {
            double R = 6371;
            var lat = (pos2.Latitude - pos1.Latitude) * (Math.PI / 180);
            var lng = (pos2.Longitude - pos1.Longitude) * (Math.PI / 180);
            var h1 = Math.Sin(lat / 2) * Math.Sin(lat / 2) +
                          Math.Cos(pos1.Latitude * (Math.PI / 180)) * Math.Cos(pos2.Latitude * (Math.PI / 180)) *
                          Math.Sin(lng / 2) * Math.Sin(lng / 2);
            var h2 = 2 * Math.Asin(Math.Min(1, Math.Sqrt(h1)));
            return R * h2;
        }

        internal static LatLng GetRectangularMinPoint(LatLng centerPoint, float distanceKilometers)
        {
            return new LatLng
            {
                Latitude = centerPoint.Latitude - (distanceKilometers * _kmToLatLngUnit),
                Longitude = centerPoint.Longitude - (distanceKilometers * _kmToLatLngUnit),
            };
        }

        internal static LatLng GetRectangularMaxPoint(LatLng centerPoint, float distanceKilometers)
        {
            return new LatLng
            {
                Latitude = centerPoint.Latitude + (distanceKilometers * _kmToLatLngUnit),
                Longitude = centerPoint.Longitude + (distanceKilometers * _kmToLatLngUnit),
            };
        }

        public static bool WithinKilometers(this LatLng latLng, LatLng centerPoint, float distanceKilometers)
        {
            ArgumentNullException.ThrowIfNull(latLng);
            ArgumentNullException.ThrowIfNull(centerPoint);

            return HaversineDistance(latLng, centerPoint) < distanceKilometers;
        }

        /// <summary>
        /// Sort the models by the nearest location. This method only has an effect when used
        /// as part of a sort expression in QueryAsync&lt;&gt;.
        /// </summary>
        public static bool Nearest(this LatLng latLng)
        {
            return false;
        }

        /// <summary>
        /// Sort the models by the furthest location. This method only has an effect when used
        /// as part of a sort expression in QueryAsync&lt;&gt;.
        /// </summary>
        public static bool Furthest(this LatLng latLng)
        {
            return false;
        }
    }
}
