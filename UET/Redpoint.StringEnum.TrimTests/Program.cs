using Redpoint.StringEnum;
using System.Collections;
using System.Diagnostics.CodeAnalysis;

AssertTrue(DynamicStringEnumValue.IsStringEnumValueType(typeof(StringEnumValue<DynamicStringEnum>)), "IsStringEnumValueType(StringEnumValue<DynamicStringEnum>)");
AssertFalse(DynamicStringEnumValue.IsStringEnumValueType(typeof(List<DynamicStringEnum>)), "IsStringEnumValueType(List<DynamicStringEnum>)");
AssertFalse(DynamicStringEnumValue.IsStringEnumValueType(typeof(object)), "IsStringEnumValueType(object)");

AssertTrue(DynamicStringEnumValue.TryParse(
    typeof(StringEnumValue<DynamicStringEnum>),
    "a",
    out var value), "TryParse(a)");
AssertTrue(ReferenceEquals(DynamicStringEnum.A, value), "ReferenceEqual(A, value)");
AssertFalse(DynamicStringEnumValue.TryParse(
    typeof(StringEnumValue<DynamicStringEnum>),
    "invalid",
    out var _), "TryParse(invalid)");

DynamicStringEnumValue.TryParse(typeof(StringEnumValue<DynamicStringEnum>), "a", out var aValue);
DynamicStringEnumValue.TryParse(typeof(StringEnumValue<DynamicStringEnum>), "b", out var bValue);
DynamicStringEnumValue.TryParse(typeof(StringEnumValue<DynamicStringEnum>), "c", out var cValue);
{
    var list = DynamicStringEnumValue.ConstructListFromValues(typeof(StringEnumValue<DynamicStringEnum>), new[] { aValue, bValue, cValue }) as IList;
    AssertTrue(list != null, "Expected ConstructListFromValues to return a list");
    AssertTrue(list.Count == 3, "Expected list to contain 3 entries.");
    AssertTrue(list[0]!.GetType() == typeof(StringEnumValue<DynamicStringEnum>), "Expected first value to be StringEnumValue<DynamicStringEnum> value.");
    AssertTrue(list[1]!.GetType() == typeof(StringEnumValue<DynamicStringEnum>), "Expected second value to be StringEnumValue<DynamicStringEnum> value.");
    AssertTrue(list[2]!.GetType() == typeof(StringEnumValue<DynamicStringEnum>), "Expected third value to be StringEnumValue<DynamicStringEnum> value.");
    AssertTrue(list[0] == aValue, "Expected first value to be 'a' value.");
    AssertTrue(list[1] == bValue, "Expected second value to be 'b' value.");
    AssertTrue(list[2] == cValue, "Expected third value to be 'c' value.");
}
{
    var array = DynamicStringEnumValue.ConstructArrayFromValues(typeof(StringEnumValue<DynamicStringEnum>), new[] { aValue, bValue, cValue }) as IList;
    AssertTrue(array != null, "Expected ConstructArrayFromValues to return an array");
    AssertTrue(array.GetType().IsArray, "Expected ConstructArrayFromValues to return an array");
    AssertTrue(array.Count == 3, "Expected array to contain 3 entries.");
    AssertTrue(array[0]!.GetType() == typeof(StringEnumValue<DynamicStringEnum>), "Expected first value to be StringEnumValue<DynamicStringEnum> value.");
    AssertTrue(array[1]!.GetType() == typeof(StringEnumValue<DynamicStringEnum>), "Expected second value to be StringEnumValue<DynamicStringEnum> value.");
    AssertTrue(array[2]!.GetType() == typeof(StringEnumValue<DynamicStringEnum>), "Expected third value to be StringEnumValue<DynamicStringEnum> value.");
    AssertTrue(array[0] == aValue, "Expected first value to be 'a' value.");
    AssertTrue(array[1] == bValue, "Expected second value to be 'b' value.");
    AssertTrue(array[2] == cValue, "Expected third value to be 'c' value.");
}

Console.WriteLine("All trim tests passed!");

void AssertTrue([DoesNotReturnIf(false)] bool expression, string errorMessage)
{
    if (!expression)
    {
        throw new InvalidOperationException($"Expected trim-test AssertTrue expression to be true: {errorMessage}");
    }
}

void AssertFalse([DoesNotReturnIf(true)] bool expression, string errorMessage)
{
    if (expression)
    {
        throw new InvalidOperationException($"Expected trim-test AssertFalse expression to be false: {errorMessage}");
    }
}

internal class DynamicStringEnum : StringEnum<DynamicStringEnum>
{
    public static readonly StringEnumValue<DynamicStringEnum> A = Create("a");
    public static readonly StringEnumValue<DynamicStringEnum> B = Create("b");
    public static readonly StringEnumValue<DynamicStringEnum> C = Create("c");
}