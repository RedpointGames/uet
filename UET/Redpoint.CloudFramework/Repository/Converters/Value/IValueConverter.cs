namespace Redpoint.CloudFramework.Repository.Converters.Value
{
    using Redpoint.CloudFramework.Models;
    using Redpoint.StringEnum;
    using Type = System.Type;
    using Value = Google.Cloud.Datastore.V1.Value;
    using Redpoint.CloudFramework.Repository.Converters.Value.Context;
    using System.Text.Json.Nodes;
    using static Google.Cloud.Datastore.V1.Value;

    /// <summary>
    /// Represents a converter that can convert CLR values into Datastore and JSON values and vice versa.
    /// </summary>
    internal interface IValueConverter
    {
        /// <summary>
        /// Returns the <see cref="FieldType"/> that this converter handles.
        /// </summary>
        /// <returns></returns>
        FieldType GetFieldType();

        /// <summary>
        /// Returns whether this converter should handle the specified CLR type. The property must already have <see cref="TypeAttribute"/> set to the result of <see cref="GetFieldType"/> for this converter to be used.
        /// </summary>
        /// <param name="clrType">The CLR type of the property on the .NET model.</param>
        /// <returns>If true, this converter should handle this property.</returns>
        bool IsConverterForClrType(Type clrType);

        /// <summary>
        /// Converts a CLR value from a [Default] attribute into the real CLR value. Non-constant
        /// expressions can not be provided to the [Default] attribute, so this allows the default
        /// constant value to be converted to the desired type for types such as <see cref="StringEnumValue{T}"/>.
        /// </summary>
        /// <remarks>
        /// This method does not receive null CLR values, as it is not possible to set null as the default via a [Default] attribute (in this case, the [Default] attribute should be omitted entirely).
        /// </remarks>
        /// <param name="context">The conversion context.</param>
        /// <param name="propertyName">The name of the property in .NET and Datastore.</param>
        /// <param name="propertyClrType">The CLR (.NET) type of the property.</param>
        /// <param name="propertyClrDefaultValue">The default value provided to the [Default] attribute.</param>
        /// <returns>The real CLR value to use as the default for model properties when Datastore or the JSON cache contains a null value.</returns>
        object? ConvertFromClrDefaultValue(
            ClrValueConvertFromContext context,
            string propertyName,
            Type propertyClrType,
            object propertyClrDefaultValue);

        /// <summary>
        /// Converts a Datastore value into a CLR value.
        /// </summary>
        /// <remarks>
        /// This method does not receive Datastore values that have <see cref="Value.IsNull"/> 
        /// set to true, as these are automatically converted to the null CLR value.
        /// </remarks>
        /// <param name="context">The conversion context.</param>
        /// <param name="propertyName">The name of the property in .NET and Datastore.</param>
        /// <param name="propertyClrType">The CLR (.NET) type of the property.</param>
        /// <param name="propertyNonNullDatastoreValue">The non-null Datastore value to convert.</param>
        /// <param name="addConvertFromDelayedLoad">If this property needs to be delay loaded, this callback allows this converter to register a delay load callback for it.</param>
        /// <returns>The CLR value for the property.</returns>
        object? ConvertFromDatastoreValue(
            DatastoreValueConvertFromContext context,
            string propertyName,
            Type propertyClrType,
            Value propertyNonNullDatastoreValue,
            AddConvertFromDelayedLoad addConvertFromDelayedLoad);

        /// <summary>
        /// Convert a CLR value into a Datastore value.
        /// </summary>
        /// <remarks>
        /// This method may receive null CLR values, as some Datastore types require setting 
        /// additional entity properties even for null values of the given <see cref="FieldType"/>.
        /// </remarks>
        /// <param name="context">The conversion context.</param>
        /// <param name="propertyName">The name of the property in .NET and Datastore.</param>
        /// <param name="propertyClrType">The CLR (.NET) type of the property.</param>
        /// <param name="propertyClrValue">The possibly null CLR (.NET) value to convert.</param>
        /// <param name="propertyIndexed">If true, this property is indexed on the entity.</param>
        /// <returns>The Datastore value to store in this property.</returns>
        Value ConvertToDatastoreValue(
            DatastoreValueConvertToContext context,
            string propertyName,
            Type propertyClrType,
            object? propertyClrValue,
            bool propertyIndexed);

        /// <summary>
        /// Converts a JSON value into a CLR value.
        /// </summary>
        /// <remarks>
        /// This method does not receive JSON values that are null, as these are automatically 
        /// converted to the null CLR value.
        /// </remarks>
        /// <param name="context">The conversion context.</param>
        /// <param name="propertyName">The name of the property in .NET and Datastore.</param>
        /// <param name="propertyClrType">The CLR (.NET) type of the property.</param>
        /// <param name="propertyNonNullJsonToken">The non-null JSON token to convert.</param>
        /// <param name="addConvertFromDelayedLoad">If this property needs to be delay loaded, this callback allows this converter to register a delay load callback for it.</param>
        /// <returns>The CLR value for the property.</returns>
        object? ConvertFromJsonToken(
            JsonValueConvertFromContext context,
            string propertyName,
            Type propertyClrType,
            JsonNode propertyNonNullJsonToken,
            AddConvertFromDelayedLoad addConvertFromDelayedLoad);

        /// <summary>
        /// Converts a CLR value into a JSON value.
        /// </summary>
        /// <remarks>
        /// Unlike <see cref="ConvertToDatastoreValue(DatastoreValueConvertToContext, string, Type, object?, bool)"/>, 
        /// this method can not receive null CLR values as they are automatically converted to the
        /// null JSON value. Value converters do not get the opportunity to set
        /// additional fields in the JSON cache for null values.
        /// </remarks>
        /// <param name="context">The conversion context.</param>
        /// <param name="propertyName">The name of the property in .NET and Datastore.</param>
        /// <param name="propertyClrType">The CLR (.NET) type of the property.</param>
        /// <param name="propertyNonNullClrValue">The non-null CLR (.NET) value to convert.</param>
        /// <returns>The JSON token to store in the cache for this property.</returns>
        JsonNode ConvertToJsonToken(
            JsonValueConvertToContext context,
            string propertyName,
            Type propertyClrType,
            object propertyNonNullClrValue);
    }
}
