namespace Redpoint.CloudFramework.Repository.Geographic
{
    using Google.Common.Geometry;
    using Google.Type;
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    internal static class S2Manager
    {
        public static ulong GenerateGeohash(LatLng geopoint)
        {
            var latLng = S2LatLng.FromDegrees(geopoint.Latitude, geopoint.Longitude);
            var cell = new S2Cell(latLng);
            return cell.Id.Id;
        }

        public static ulong GenerateGeohashKey(ulong geohash, int geohashKeyLength)
        {
            var geohashString = geohash.ToString(CultureInfo.InvariantCulture);
            var denominator = (ulong)Math.Pow(10, geohashString.Length - geohashKeyLength);
            return geohash / denominator;
        }

        public static S2LatLngRect LatLngRectFromQueryRectangleInput(LatLng minPoint, LatLng maxPoint)
        {
            return S2LatLngRect.FromPointPair(
                S2LatLng.FromDegrees(minPoint.Latitude, minPoint.Longitude),
                S2LatLng.FromDegrees(maxPoint.Latitude, maxPoint.Longitude)
            );
        }

        public struct GeohashRange
        {
            public ulong RangeMin;

            public ulong RangeMax;
        }

        public static GeohashRange[] GetGeohashRanges(S2LatLngRect rect, int geohashKeyLength)
        {
            var ranges = new List<GeohashRange>();
            var covering = new S2RegionCoverer().GetCovering(rect);
            foreach (var outerRange in covering)
            {
                var rangeMin = outerRange.RangeMin.Id;
                var rangeMax = outerRange.RangeMax.Id;
                var minHashKey = S2Manager.GenerateGeohashKey(rangeMin, geohashKeyLength);
                var maxHashKey = S2Manager.GenerateGeohashKey(rangeMax, geohashKeyLength);
                var denominator = (ulong)Math.Pow(10, rangeMin.ToString(CultureInfo.InvariantCulture).Length - minHashKey.ToString(CultureInfo.InvariantCulture).Length);

                if (minHashKey.Equals(maxHashKey))
                {
                    ranges.Add(new GeohashRange { RangeMin = rangeMin, RangeMax = rangeMax });
                }
                else
                {
                    for (var l = minHashKey; l <= maxHashKey; l++)
                    {
                        if (l > 0)
                        {
                            ranges.Add(new GeohashRange {
                                RangeMin= l == minHashKey ? rangeMin : (l * denominator),
RangeMax=                                 l == maxHashKey ? rangeMax : (((l + 1) * denominator) - 1)
                            });
                        }
                        else
                        {
                            ranges.Add(new GeohashRange {
                                RangeMin = l == minHashKey ? rangeMin : (((l - 1) * denominator) + 1),
                                RangeMax = l == maxHashKey ? rangeMax : (l * denominator)
                            });
                        }
                    }
                }
            }
            return ranges.ToArray();
        }

        // TOOD: filterByRectangle
        // https://github.com/damack/datastore-geo/blob/master/src/S2Manager.js
    }
}
