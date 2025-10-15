namespace Redpoint.CloudFramework.Repository.Converters.Value
{
    using System;

    public class JsonValueWasInvalidException : Exception
    {
        public JsonValueWasInvalidException(string propertyName)
            : base($"The value of the '{propertyName}' property was invalid.")
        {
        }
    }
}
