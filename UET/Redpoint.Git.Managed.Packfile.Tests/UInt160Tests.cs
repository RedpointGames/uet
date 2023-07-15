namespace Redpoint.Git.Managed.Packfile.Tests
{
    using System.Runtime.Intrinsics;

#pragma warning disable CS1718 // Comparison made to same variable

    public class UInt160Tests
    {
        [Fact]
        public void RoundTrip()
        {
            var hash = UInt160.CreateFromString("d6340facfbb763c6d516e7599eac94245fce52ec");
            var str = hash.ToString();
            Assert.Equal("d6340facfbb763c6d516e7599eac94245fce52ec", str);
        }

        [Fact]
        public void SimpleLessThan()
        {
            var sl = UInt160.CreateFromString("0000000000000000000000000000000000000001");
            var se = UInt160.CreateFromString("0000000000000000000000000000000000000002");

            Assert.True(sl < se);
        }

        [Fact]
        public void Equal()
        {
            var bl = UInt160.CreateFromString("d6340facfbb763c6c516e7599eac94245fce52ec");
            var sl = UInt160.CreateFromString("d6340facfbb763c6d516e7599eac94245fce52eb");
            var se = UInt160.CreateFromString("d6340facfbb763c6d516e7599eac94245fce52ec");
            var sh = UInt160.CreateFromString("d6340facfbb763c6d516e7599eac94245fce52ed");
            var bh = UInt160.CreateFromString("d6340facfbb763c6e516e7599eac94245fce52ec");

            Assert.False(bl == se);
            Assert.False(sl == se);
            Assert.True(se == se);
            Assert.False(sh == se);
            Assert.False(bh == se);
        }

        [Fact]
        public void NotEqual()
        {
            var bl = UInt160.CreateFromString("d6340facfbb763c6c516e7599eac94245fce52ec");
            var sl = UInt160.CreateFromString("d6340facfbb763c6d516e7599eac94245fce52eb");
            var se = UInt160.CreateFromString("d6340facfbb763c6d516e7599eac94245fce52ec");
            var sh = UInt160.CreateFromString("d6340facfbb763c6d516e7599eac94245fce52ed");
            var bh = UInt160.CreateFromString("d6340facfbb763c6e516e7599eac94245fce52ec");

            Assert.True(bl != se);
            Assert.True(sl != se);
            Assert.False(se != se);
            Assert.True(sh != se);
            Assert.True(bh != se);
        }

        [Fact]
        public void LessThan()
        {
            var bl = UInt160.CreateFromString("d6340facfbb763c6c516e7599eac94245fce52ec");
            var sl = UInt160.CreateFromString("d6340facfbb763c6d516e7599eac94245fce52eb");
            var se = UInt160.CreateFromString("d6340facfbb763c6d516e7599eac94245fce52ec");
            var sh = UInt160.CreateFromString("d6340facfbb763c6d516e7599eac94245fce52ed");
            var bh = UInt160.CreateFromString("d6340facfbb763c6e516e7599eac94245fce52ec");

            Assert.True(bl < se);
            Assert.True(sl < se);
            Assert.False(se < se);
            Assert.False(sh < se);
            Assert.False(bh < se);
        }

        [Fact]
        public void LessThanOrEqual()
        {
            var bl = UInt160.CreateFromString("d6340facfbb763c6c516e7599eac94245fce52ec");
            var sl = UInt160.CreateFromString("d6340facfbb763c6d516e7599eac94245fce52eb");
            var se = UInt160.CreateFromString("d6340facfbb763c6d516e7599eac94245fce52ec");
            var sh = UInt160.CreateFromString("d6340facfbb763c6d516e7599eac94245fce52ed");
            var bh = UInt160.CreateFromString("d6340facfbb763c6e516e7599eac94245fce52ec");

            Assert.True(bl <= se);
            Assert.True(sl <= se);
            Assert.True(se <= se);
            Assert.False(sh <= se);
            Assert.False(bh <= se);
        }

        [Fact]
        public void GreaterThan()
        {
            var bl = UInt160.CreateFromString("d6340facfbb763c6c516e7599eac94245fce52ec");
            var sl = UInt160.CreateFromString("d6340facfbb763c6d516e7599eac94245fce52eb");
            var se = UInt160.CreateFromString("d6340facfbb763c6d516e7599eac94245fce52ec");
            var sh = UInt160.CreateFromString("d6340facfbb763c6d516e7599eac94245fce52ed");
            var bh = UInt160.CreateFromString("d6340facfbb763c6e516e7599eac94245fce52ec");

            Assert.False(bl > se);
            Assert.False(sl > se);
            Assert.False(se > se);
            Assert.True(sh > se);
            Assert.True(bh > se);
        }

        [Fact]
        public void GreaterThanOrEqual()
        {
            var bl = UInt160.CreateFromString("d6340facfbb763c6c516e7599eac94245fce52ec");
            var sl = UInt160.CreateFromString("d6340facfbb763c6d516e7599eac94245fce52eb");
            var se = UInt160.CreateFromString("d6340facfbb763c6d516e7599eac94245fce52ec");
            var sh = UInt160.CreateFromString("d6340facfbb763c6d516e7599eac94245fce52ed");
            var bh = UInt160.CreateFromString("d6340facfbb763c6e516e7599eac94245fce52ec");

            Assert.False(bl >= se);
            Assert.False(sl >= se);
            Assert.True(se >= se);
            Assert.True(sh >= se);
            Assert.True(bh >= se);
        }

        [Fact]
        public void CompareTo()
        {
            var tl = UInt160.CreateFromString("c6340facfbb763c6d516e7599eac94245fce52ec");
            var bl = UInt160.CreateFromString("d6340facfbb763c6c516e7599eac94245fce52ec");
            var sl = UInt160.CreateFromString("d6340facfbb763c6d516e7599eac94245fce52eb");
            var se = UInt160.CreateFromString("d6340facfbb763c6d516e7599eac94245fce52ec");
            var sh = UInt160.CreateFromString("d6340facfbb763c6d516e7599eac94245fce52ed");
            var bh = UInt160.CreateFromString("d6340facfbb763c6e516e7599eac94245fce52ec");
            var th = UInt160.CreateFromString("e6340facfbb763c6d516e7599eac94245fce52ec");

            Assert.Equal(-1, tl.CompareTo(se));
            Assert.Equal(-1, bl.CompareTo(se));
            Assert.Equal(-1, sl.CompareTo(se));
            Assert.Equal(0, se.CompareTo(se));
            Assert.Equal(1, sh.CompareTo(se));
            Assert.Equal(1, bh.CompareTo(se));
            Assert.Equal(1, th.CompareTo(se));
        }
    }

#pragma warning restore CS1718 // Comparison made to same variable
}
