namespace Redpoint.CloudFramework.Repository.Converters.Value
{
    using Redpoint.CloudFramework.Models;
    using System;

    internal interface IValueConverterProvider
    {
        IValueConverter GetConverter(
            FieldType fieldType,
            Type propertyClrType);
    }
}
