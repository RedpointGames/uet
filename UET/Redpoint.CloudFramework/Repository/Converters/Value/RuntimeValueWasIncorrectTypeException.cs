namespace Redpoint.CloudFramework.Repository.Converters.Value
{
    using System;

    public class RuntimeValueWasIncorrectTypeException : Exception
    {
        public RuntimeValueWasIncorrectTypeException(string propertyName, object value, Type expectedType)
            : base($"The value of the '{propertyName}' property has a value of type '{value?.GetType()?.FullName}', but this code expects values with a type '{expectedType?.FullName}'.")
        {
        }
    }
}
