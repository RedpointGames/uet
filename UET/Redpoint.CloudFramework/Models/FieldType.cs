namespace Redpoint.CloudFramework.Models
{
    public enum FieldType
    {
#pragma warning disable CA1720 // Identifier contains type name
        String,
        Boolean,
        Integer,
        Double,
        Geopoint,
        Key,
        LocalKey,
        GlobalKey,
        UnsafeKey,
        Timestamp,
        Json,
        File,
        StringArray,
        KeyArray,
        EmbeddedEntity,
        GlobalKeyArray,
        UnsignedInteger,
        UnsignedIntegerArray,
#pragma warning restore CA1720 // Identifier contains type name
    }
}
