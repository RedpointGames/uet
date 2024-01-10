namespace Redpoint.StringEnum.Tests
{
    using Xunit;

#pragma warning disable CA1711 // Identifiers should not have incorrect suffix
#pragma warning disable CS1718 // Comparison made to same variable

    internal class TestStringEnum : StringEnum<TestStringEnum>
    {
        public static readonly StringEnumValue<TestStringEnum> A = Create("a");
        public static readonly StringEnumValue<TestStringEnum> B = Create("b");
        public static readonly StringEnumValue<TestStringEnum> C = Create("c");
    }

    public class StringEnumTests
    {
        private static readonly string[] _expectedPermittedValues = new[] { "a", "b", "c" };

        [Fact]
        public void CanConvertEnumValuesToStrings()
        {
            Assert.Equal("a", TestStringEnum.A);
            Assert.Equal("b", TestStringEnum.B);
            Assert.Equal("c", TestStringEnum.C);
        }

        [Fact]
        public void EnumValuesAreEqual()
        {
            Assert.Equal(TestStringEnum.A, TestStringEnum.A);
            Assert.True(TestStringEnum.A == TestStringEnum.A, "Equality operator should return true");
            Assert.True(TestStringEnum.A.Equals(TestStringEnum.A), "Equals function should return true");
            Assert.Same(TestStringEnum.A, TestStringEnum.A);
        }

        [Fact]
        public void ParsedEnumValuesAreEqual()
        {
            var a1 = TestStringEnum.Parse("a");
            var a2 = TestStringEnum.Parse("a");

            Assert.Equal(a1, a2);
            Assert.True(a1 == a2, "Equality operator should return true");
            Assert.True(a1.Equals(a2), "Equals function should return true");
            Assert.Same(a1, a2);
        }

        [Fact]
        public void DifferentEnumValuesAreNotEqual()
        {
            Assert.NotEqual(TestStringEnum.A, TestStringEnum.B);
            Assert.False(TestStringEnum.A == TestStringEnum.B, "Equality operator should return false");
            Assert.True(TestStringEnum.A != TestStringEnum.B, "Inequality operator should return true");
            Assert.False(TestStringEnum.A.Equals(TestStringEnum.B), "Equals function should return true");
            Assert.NotSame(TestStringEnum.A, TestStringEnum.B);
        }

        [Fact]
        public void DifferentParsedEnumValuesAreEqual()
        {
            var a = TestStringEnum.Parse("a");
            var b = TestStringEnum.Parse("b");

            Assert.NotEqual(a, b);
            Assert.False(a == b, "Equality operator should return true");
            Assert.True(a != b, "Inequality operator should return true");
            Assert.False(a.Equals(b), "Equals function should return true");
            Assert.NotSame(a, b);
        }

        [Fact]
        public void ParseWithInvalidOptionThrows()
        {
            var ex = Assert.Throws<StringEnumParseException>(() =>
            {
                TestStringEnum.Parse("invalid");
            });
            Assert.Equal(typeof(TestStringEnum), ex.EnumerationType);
            Assert.Equal("invalid", ex.ReceivedValue);
            Assert.Equal(_expectedPermittedValues, ex.PermittedValues);
        }

        [Fact]
        public void TryParseReturnsCorrectValuesForParseAttempts()
        {
            Assert.True(TestStringEnum.TryParse("a", out _));
            Assert.True(TestStringEnum.TryParse("b", out _));
            Assert.True(TestStringEnum.TryParse("c", out _));
            Assert.False(TestStringEnum.TryParse("", out _));
            Assert.False(TestStringEnum.TryParse("invalid", out _));
        }

        [Fact]
        public void ArgumentNullExceptionThrownWhenExpected()
        {
            Assert.Throws<ArgumentNullException>(() => TestStringEnum.Parse(null!));
            Assert.Throws<ArgumentNullException>(() => TestStringEnum.TryParse(null!, out _));
        }
    }

#pragma warning restore CS1718 // Comparison made to same variable
#pragma warning restore CA1711 // Identifiers should not have incorrect suffix
}
