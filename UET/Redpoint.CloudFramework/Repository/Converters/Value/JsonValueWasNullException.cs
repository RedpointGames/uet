namespace Redpoint.CloudFramework.Repository.Converters.Value
{
    using System;

    public class JsonValueWasNullException : Exception
    {
        public JsonValueWasNullException(string propertyName)
            : base($"The value of the '{propertyName}' property was null in JSON, but this code should not be handling null values.")
        {
        }
    }
}
