namespace Redpoint.StringEnum
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Thrown when calling <see cref="StringEnumValue{T}.Parse(string)"/> on a string value, and the string
    /// value is not a valid value for the enumeration type.
    /// </summary>
    public sealed class StringEnumParseException : Exception
    {
        internal StringEnumParseException(
            Type enumerationType,
            string receivedValue,
            IReadOnlyList<string> permittedValues)
            : base($"The string-based enumeration value of '{receivedValue}' could not be parsed for the type '{enumerationType.FullName ?? "(unknown type)"}' because the only permitted values are '{string.Join(", ", permittedValues)}'.")
        {
            EnumerationType = enumerationType;
            ReceivedValue = receivedValue;
            PermittedValues = permittedValues;
        }

        /// <summary>
        /// The derived type (T parameter) of <see cref="StringEnum{T}"/> that holds all the possible values.
        /// </summary>
        public Type EnumerationType { get; }

        /// <summary>
        /// The value that was received to <see cref="StringEnumValue{T}"/>.
        /// </summary>
        public string ReceivedValue { get; }

        /// <summary>
        /// The list of values defined by the fields and properties of <see cref="EnumerationType"/>.
        /// </summary>
        public IReadOnlyList<string> PermittedValues { get; }
    }
}
