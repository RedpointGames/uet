namespace Redpoint.StringEnum
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;

    /// <summary>
    /// The base class for all string-backed enumeration types. You should inherit from this class so
    /// you have access to the protected static <see cref="Create(string)"/> function. The properties
    /// and fields of your derived type are used as all the possible values for the enumeration type.
    /// </summary>
    /// <typeparam name="T">The derived type that inherits from <see cref="StringEnum{T}"/>.</typeparam>
#pragma warning disable CA1711 // This really does represent an enumeration; one backed by strings.
#pragma warning disable CA1052 // We need this class to be non-static so you can inherit from it and access Create.
#pragma warning disable CA1724 // This is effectively a "single class" library, so it's fine that the class matches the namespace name.
    public class StringEnum<[DynamicallyAccessedMembers(
#pragma warning restore CA1724
#pragma warning restore CA1052
#pragma warning restore CA1711
        DynamicallyAccessedMemberTypes.PublicProperties |
        DynamicallyAccessedMemberTypes.NonPublicProperties |
        DynamicallyAccessedMemberTypes.PublicFields |
        DynamicallyAccessedMemberTypes.NonPublicFields |
        DynamicallyAccessedMemberTypes.PublicMethods |
        DynamicallyAccessedMemberTypes.NonPublicMethods)] T> where T : StringEnum<T>
    {
        internal static Lazy<IReadOnlyDictionary<string, StringEnumValue<T>>> _values = new Lazy<IReadOnlyDictionary<string, StringEnumValue<T>>>(() =>
        {
            var values = new Dictionary<string, StringEnumValue<T>>(StringComparer.Ordinal);
            foreach (var prop in typeof(T).GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static))
            {
                if (prop.PropertyType == typeof(StringEnumValue<T>))
                {
                    var value = (StringEnumValue<T>)prop.GetValue(null)!;
                    values.Add(value.Value, value);
                }
            }
            foreach (var field in typeof(T).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static))
            {
                if (field.FieldType == typeof(StringEnumValue<T>))
                {
                    var value = (StringEnumValue<T>)field.GetValue(null)!;
                    values.Add(value.Value, value);
                }
            }
            return values;
        });

        /// <summary>
        /// Create a new possible value for this string-backed enumeration type. String values are
        /// case and byte sensitive (using ordinal comparison).
        /// </summary>
        /// <param name="value">The string value to be stored in database and other external systems.</param>
        /// <returns>The new <see cref="StringEnumValue{T}"/> instance that represents this value everywhere.</returns>
        protected static StringEnumValue<T> Create(string value)
        {
            ArgumentNullException.ThrowIfNull(value);
            return new StringEnumValue<T>(value);
        }

        /// <summary>
        /// Parse a string value back into a <see cref="StringEnumValue{T}"/>, throwing <see cref="StringEnumParseException"/> if the provided value isn't a valid enumeration value.
        /// </summary>
        /// <remarks>
        /// This method forwards the call to <see cref="StringEnumValue{T}.Parse(string)" />. Both calls behave exactly the same.
        /// </remarks>
        /// <param name="value">The string value to parse.</param>
        /// <returns>The enumeration value that the provided string represents.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="value"/> is null, since null values are never valid enumeration values.</exception>
        /// <exception cref="StringEnumParseException">Thrown if the provided string value does not map to any known enumeration value.</exception>
        public static StringEnumValue<T> Parse(string value)
        {
            return StringEnumValue<T>.Parse(value);
        }

        /// <summary>
        /// Attempts to parse a string value back into a <see cref="StringEnumValue{T}"/>, returning whether it was successful and the value in <paramref name="result"/> if it was.
        /// </summary>
        /// <remarks>
        /// This method forwards the call to <see cref="StringEnumValue{T}.TryParse(string, out StringEnumValue{T}?)" />. Both calls behave exactly the same.
        /// </remarks>
        /// <param name="value">The string value to parse.</param>
        /// <param name="result">The resulting enumeration value if parsing was successful.</param>
        /// <returns>If the string value could be successfully parsed back into an enumeration value.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="value"/> is null, since null values are never valid enumeration values.</exception>
        public static bool TryParse(string value, [NotNullWhen(true)] out StringEnumValue<T>? result)
        {
            return StringEnumValue<T>.TryParse(value, out result);
        }

        /// <summary>
        /// Constructs an instance of <see cref="List{T}"/> from a set of values, where those values
        /// may or may not be <see cref="StringEnumValue{T}"/> values. Any invalid values are ignored.
        /// </summary>
        /// <returns>A new list instance.</returns>
        internal static List<StringEnumValue<T>> ConstructListFromValues(IEnumerable values)
        {
            return values
                .OfType<StringEnumValue<T>>()
                .ToList();
        }

        /// <summary>
        /// Constructs a <typeparamref name="T"/> array from a set of values, where those values
        /// may or may not be <see cref="StringEnumValue{T}"/> values. Any invalid values are ignored.
        /// </summary>
        /// <returns>A new list instance.</returns>
        internal static StringEnumValue<T>[] ConstructArrayFromValues(IEnumerable values)
        {
            return values
                .OfType<StringEnumValue<T>>()
                .ToArray();
        }

        /// <summary>
        /// Constructs an instance of <see cref="HashSet{T}"/> from a set of values, where those values
        /// may or may not be <see cref="StringEnumValue{T}"/> values. Any invalid values are ignored.
        /// </summary>
        /// <returns>A new set instance.</returns>
        internal static HashSet<StringEnumValue<T>> ConstructSetFromValues(IEnumerable values)
        {
            return values
                .OfType<StringEnumValue<T>>()
                .ToHashSet();
        }
    }
}
