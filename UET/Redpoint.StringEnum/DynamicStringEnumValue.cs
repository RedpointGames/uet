namespace Redpoint.StringEnum
{
    using System;
    using System.Collections;
    using System.Diagnostics.CodeAnalysis;
    using System.Reflection;

    /// <summary>
    /// Provides a method for calling <see cref="StringEnumValue{T}.TryParse(string, out StringEnumValue{T}?)"/> on a runtime type
    /// (i.e. where you don't know the particular <see cref="StringEnum{T}"/> type in use at compile time).
    /// </summary>
    public static class DynamicStringEnumValue
    {
        /// <summary>
        /// Returns whether a type meets the requirements for calling <see cref="TryParse(Type, string, out object?)"/>.
        /// </summary>
        /// <param name="type">The runtime type that should be a constructed generic instance of <see cref="StringEnumValue{T}"/>.</param>
        /// <returns>True if the runtime type is a constructed generic instance of <see cref="StringEnumValue{T}"/>.</returns>
        public static bool IsStringEnumValueType(Type type)
        {
            ArgumentNullException.ThrowIfNull(type);
            return type.IsConstructedGenericType &&
                type.GetGenericTypeDefinition() == typeof(StringEnumValue<>);
        }

        /// <summary>
        /// Constructs a list type that holds the specified string enumeration values.
        /// </summary>
        /// <param name="stringEnumValueType">The <see cref="StringEnumValue{T}"/> element type to hold.</param>
        /// <param name="values">The <see cref="StringEnumValue{T}"/> values to store in the new list instance.</param>
        /// <returns>The new list instance.</returns>
        [UnconditionalSuppressMessage("Trimming", "IL2065:The method has a DynamicallyAccessedMembersAttribute (which applies to the implicit 'this' parameter), but the value used for the 'this' parameter can not be statically analyzed.", Justification = "We know this method is kept because we use DynamicallyAccessedMembersAttribute on StringEnumValue<T> to keep it.")]
        public static object ConstructListFromValues(
            Type stringEnumValueType,
            IEnumerable values)
        {
            ArgumentNullException.ThrowIfNull(stringEnumValueType);
            ArgumentNullException.ThrowIfNull(values);
            if (!IsStringEnumValueType(stringEnumValueType))
            {
                throw new ArgumentException("You must provide a type definition of StringEnumValue<T> to call this method.");
            }

            var stringEnumType = stringEnumValueType.GetGenericArguments()[0];
            var constructListFromValuesMethod = stringEnumType.GetMethod(
                "ConstructListFromValues",
                BindingFlags.Static |
                BindingFlags.NonPublic |
                BindingFlags.FlattenHierarchy);
            if (constructListFromValuesMethod == null)
            {
                throw new BadImageFormatException("T.ConstructListFromValues of StringEnumValue<T> was unexpectedly trimmed from the resulting assembly!");
            }

            return constructListFromValuesMethod.Invoke(
                null,
                BindingFlags.DoNotWrapExceptions,
                null,
                new object?[] { values },
                null)!;
        }

        /// <summary>
        /// Constructs an array type that holds the specified string enumeration values.
        /// </summary>
        /// <param name="stringEnumValueType">The <see cref="StringEnumValue{T}"/> element type to hold.</param>
        /// <param name="values">The <see cref="StringEnumValue{T}"/> values to store in the new list instance.</param>
        /// <returns>The new list instance.</returns>
        [UnconditionalSuppressMessage("Trimming", "IL2065:The method has a DynamicallyAccessedMembersAttribute (which applies to the implicit 'this' parameter), but the value used for the 'this' parameter can not be statically analyzed.", Justification = "We know this method is kept because we use DynamicallyAccessedMembersAttribute on StringEnumValue<T> to keep it.")]
        public static object ConstructArrayFromValues(
            Type stringEnumValueType,
            IEnumerable values)
        {
            ArgumentNullException.ThrowIfNull(stringEnumValueType);
            ArgumentNullException.ThrowIfNull(values);
            if (!IsStringEnumValueType(stringEnumValueType))
            {
                throw new ArgumentException("You must provide a type definition of StringEnumValue<T> to call this method.");
            }

            var stringEnumType = stringEnumValueType.GetGenericArguments()[0];
            var constructArrayFromValuesMethod = stringEnumType.GetMethod(
                "ConstructArrayFromValues",
                BindingFlags.Static |
                BindingFlags.NonPublic |
                BindingFlags.FlattenHierarchy);
            if (constructArrayFromValuesMethod == null)
            {
                throw new BadImageFormatException("T.ConstructArrayFromValues of StringEnumValue<T> was unexpectedly trimmed from the resulting assembly!");
            }

            return constructArrayFromValuesMethod.Invoke(
                null,
                BindingFlags.DoNotWrapExceptions,
                null,
                new object?[] { values },
                null)!;
        }


        /// <summary>
        /// Constructs a set type that holds the specified string enumeration values.
        /// </summary>
        /// <param name="stringEnumValueType">The <see cref="StringEnumValue{T}"/> element type to hold.</param>
        /// <param name="values">The <see cref="StringEnumValue{T}"/> values to store in the new list instance.</param>
        /// <returns>The new list instance.</returns>
        [UnconditionalSuppressMessage("Trimming", "IL2065:The method has a DynamicallyAccessedMembersAttribute (which applies to the implicit 'this' parameter), but the value used for the 'this' parameter can not be statically analyzed.", Justification = "We know this method is kept because we use DynamicallyAccessedMembersAttribute on StringEnumValue<T> to keep it.")]
        public static object ConstructSetFromValues(
            Type stringEnumValueType,
            IEnumerable values)
        {
            ArgumentNullException.ThrowIfNull(stringEnumValueType);
            ArgumentNullException.ThrowIfNull(values);
            if (!IsStringEnumValueType(stringEnumValueType))
            {
                throw new ArgumentException("You must provide a type definition of StringEnumValue<T> to call this method.");
            }

            var stringEnumType = stringEnumValueType.GetGenericArguments()[0];
            var constructSetFromValuesMethod = stringEnumType.GetMethod(
                "ConstructSetFromValues",
                BindingFlags.Static |
                BindingFlags.NonPublic |
                BindingFlags.FlattenHierarchy);
            if (constructSetFromValuesMethod == null)
            {
                throw new BadImageFormatException("T.ConstructArrayFromValues of StringEnumValue<T> was unexpectedly trimmed from the resulting assembly!");
            }

            return constructSetFromValuesMethod.Invoke(
                null,
                BindingFlags.DoNotWrapExceptions,
                null,
                new object?[] { values },
                null)!;
        }

        /// <summary>
        /// Attempts to parse a string value back into a <see cref="StringEnumValue{T}"/>, where the string enumeration type
        /// is defined by the runtime value <paramref name="stringEnumValueType"/>, returning whether the parse was successful 
        /// and the value in <paramref name="result"/> if it was.
        /// </summary>
        /// <param name="stringEnumValueType">The string enumeration type that the parse should occur for; this must be a constructed type of <see cref="StringEnumValue{T}"/>.</param>
        /// <param name="value">The string value to parse.</param>
        /// <param name="result">The resulting enumeration value if parsing was successful.</param>
        /// <returns>If the string value could be successfully parsed back into an enumeration value.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="value"/> is null, since null values are never valid enumeration values.</exception>
        [UnconditionalSuppressMessage("Trimming", "IL2065:The method has a DynamicallyAccessedMembersAttribute (which applies to the implicit 'this' parameter), but the value used for the 'this' parameter can not be statically analyzed.", Justification = "We know this method is kept because we use DynamicallyAccessedMembersAttribute on StringEnumValue<T> to keep it.")]
        public static bool TryParse(
            Type stringEnumValueType,
            string value,
            [NotNullWhen(true)] out object? result)
        {
            ArgumentNullException.ThrowIfNull(stringEnumValueType);
            ArgumentNullException.ThrowIfNull(value);
            if (!IsStringEnumValueType(stringEnumValueType))
            {
                throw new ArgumentException("You must provide a type definition of StringEnumValue<T> to call this method.");
            }

            var stringEnumType = stringEnumValueType.GetGenericArguments()[0];
            var tryParseMethod = stringEnumType.GetMethod(
                "TryParse",
                BindingFlags.Static |
                BindingFlags.Public |
                BindingFlags.FlattenHierarchy);
            if (tryParseMethod == null)
            {
                // @note: This method should not get trimmed because we use [DynamicallyAccessedMembers] to keep it.
                throw new BadImageFormatException("T.TryParse of StringEnumValue<T> was unexpectedly trimmed from the resulting assembly!");
            }

            var argumentArray = new object?[] { value, null };
            var successfulParse = (bool)tryParseMethod.Invoke(null, BindingFlags.DoNotWrapExceptions, null, argumentArray, null)!;
            result = argumentArray[1];
            return successfulParse;
        }
    }
}
