namespace Redpoint.CloudFramework.Models
{
    using System;

    /// <summary>
    /// Specifies the hash key length for Geopoint fields. This attribute
    /// must be specified on all Geopoint fields if you're using AttributedModel.
    /// </summary>
    /// <remarks>
    /// The hash key length effectively determines the "granularity" of indexed
    /// data.
    /// <br /><br />
    /// A larger hash key length will spread small geographic areas over lots of
    /// partitions at the cost of more queries being performed for larger search
    /// radii. A smaller hash key will mean that more entities have the same hash key,
    /// and thus more entities will be filtered out server side (after having
    /// been returned from Datastore).
    /// <br /><br />
    /// As a guide for choosing a value:
    /// <br /><br />
    /// - A value of 6, with a radius of 35km will query 38 partitions.<br />
    /// - A value of 2, with a radius of 35km will query 8 partitions.
    /// <br /><br />
    /// If you have a global application, and you're unsure of a value to choose,
    /// we've found a value of 2 is a good balance for an application with a relatively
    /// low number of entries spread over the entire planet.
    /// <br /><br />
    /// The hash key length can not be changed later without recreating your data.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class GeopointAttribute : Attribute
    {
        /// <summary>
        /// Constructs a geopoint attribute.
        /// </summary>
        /// <param name="hashKeyLength">
        /// The hash key length effectively determines the "granularity" of indexed
        /// data.
        /// <br /><br />
        /// A larger hash key length will spread small geographic areas over lots of
        /// partitions at the cost of more queries being performed for larger search
        /// radii. A smaller hash key will mean that more entities have the same hash key,
        /// and thus more entities will be filtered out server side (after having
        /// been returned from Datastore).
        /// <br /><br />
        /// As a guide for choosing a value:
        /// <br /><br />
        /// - A value of 6, with a radius of 35km will query 38 partitions.<br />
        /// - A value of 2, with a radius of 35km will query 8 partitions.
        /// <br /><br />
        /// If you have a global application, and you're unsure of a value to choose,
        /// we've found a value of 2 is a good balance for an application with a relatively
        /// low number of entries spread over the entire planet.
        /// <br /><br />
        /// The hash key length can not be changed later without recreating your data.
        /// </param>
        public GeopointAttribute(ushort hashKeyLength)
        {
            HashKeyLength = hashKeyLength;
        }

        public ushort HashKeyLength { get; }
    }
}
