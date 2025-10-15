namespace Redpoint.CloudFramework.Repository.Converters.Value
{
    using System;

    public class RuntimeValueWasNullException : Exception
    {
        public RuntimeValueWasNullException(string propertyName)
            : base($"The value of the '{propertyName}' property was null at runtime, but this code should not be handling null values.")
        {
        }
    }
}
