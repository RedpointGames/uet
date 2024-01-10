namespace Redpoint.StringEnum
{
    using System;
    using System.Diagnostics.CodeAnalysis;

    /// <summary>
    /// Represents a value of a string-backed enumeration type. You can not manually create
    /// values of this type; you must use <see cref="StringEnum{T}.Create(string)" /> when
    /// defining possible values, and <see cref="Parse(string)"/> to parse arbitrary string
    /// values back into enumeration values.
    /// </summary>
    /// <typeparam name="T">The derived type of <see cref="StringEnum{T}"/> that contains possible enumeration values.</typeparam>
    public sealed record class StringEnumValue<[DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicProperties |
        DynamicallyAccessedMemberTypes.NonPublicProperties |
        DynamicallyAccessedMemberTypes.PublicFields |
        DynamicallyAccessedMemberTypes.NonPublicFields |
        // @note: PublicMethods and NonPublicMethods are required so that
        // DynamicStringEnumValue can do it's work. The tests in
        // Redpoint.StringEnum.TrimTests are used to ensure that DynamicStringEnumValue
        // works correctly (since it can't be tested from Xunit).
        DynamicallyAccessedMemberTypes.PublicMethods |
        DynamicallyAccessedMemberTypes.NonPublicMethods)] T> where T : StringEnum<T>
    {
        internal StringEnumValue(string value)
        {
            ArgumentNullException.ThrowIfNull(value);
            Value = value;
        }

        /// <summary>
        /// The string value that represents this enumeration value.
        /// </summary>
        public string Value { get; }

        /// <summary>
        /// Returns the string value that represents this enumeration value.
        /// </summary>
        /// <returns>The string value that represents this enumeration value.</returns>
        public override string ToString()
        {
            return Value;
        }

        /// <summary>
        /// Implicitly converts this enumeration value to it's string representation.
        /// </summary>
        /// <param name="v">The enumeration value.</param>
        public static implicit operator string(StringEnumValue<T> v)
        {
            ArgumentNullException.ThrowIfNull(v);
            return v.Value;
        }

        /// <summary>
        /// Parse a string value back into a <see cref="StringEnumValue{T}"/>, throwing <see cref="StringEnumParseException"/> if the provided value isn't a valid enumeration value.
        /// </summary>
        /// <param name="value">The string value to parse.</param>
        /// <returns>The enumeration value that the provided string represents.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="value"/> is null, since null values are never valid enumeration values.</exception>
        /// <exception cref="StringEnumParseException">Thrown if the provided string value does not map to any known enumeration value.</exception>
        public static StringEnumValue<T> Parse(string value)
        {
            ArgumentNullException.ThrowIfNull(value);
            if (StringEnum<T>._values.Value.TryGetValue(value, out var result))
            {
                return result;
            }
            else
            {
                throw new StringEnumParseException(
                    typeof(T),
                    value,
                    StringEnum<T>._values.Value.Keys.ToList());
            }
        }

        /// <summary>
        /// Attempts to parse a string value back into a <see cref="StringEnumValue{T}"/>, returning whether it was successful and the value in <paramref name="result"/> if it was.
        /// </summary>
        /// <param name="value">The string value to parse.</param>
        /// <param name="result">The resulting enumeration value if parsing was successful.</param>
        /// <returns>If the string value could be successfully parsed back into an enumeration value.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="value"/> is null, since null values are never valid enumeration values.</exception>
        public static bool TryParse(string value, [NotNullWhen(true)] out StringEnumValue<T>? result)
        {
            ArgumentNullException.ThrowIfNull(value);
            return StringEnum<T>._values.Value.TryGetValue(value, out result);
        }
    }
}
