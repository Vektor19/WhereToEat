using FluentAssertions;
using WhereToEat.SharedKernel.Primitives;
using Xunit;

namespace WhereToEat.SharedKernel.UnitTests.Primitives;

public sealed class ValueObjectTests
{
    // Two small concrete value objects exercise the base equality contract directly,
    // independently of Money/GeoPoint (which add their own validation on top).
    private sealed class Pair : ValueObject
    {
        public Pair(int x, string y)
        {
            X = x;
            Y = y;
        }

        public int X { get; }

        public string Y { get; }

        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return X;
            yield return Y;
        }
    }

    // A different concrete type with the *same* component shape — must never equal a Pair.
    private sealed class OtherPair : ValueObject
    {
        public OtherPair(int x, string y)
        {
            X = x;
            Y = y;
        }

        public int X { get; }

        public string Y { get; }

        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return X;
            yield return Y;
        }
    }

    [Fact]
    public void Equals_WhenAllComponentsMatch_IsTrue()
    {
        var a = new Pair(1, "a");
        var b = new Pair(1, "a");

        a.Equals(b).Should().BeTrue();
        a.Equals((object)b).Should().BeTrue();
        (a == b).Should().BeTrue();
        (a != b).Should().BeFalse();
    }

    [Theory]
    [InlineData(2, "a")]   // first component differs
    [InlineData(1, "b")]   // second component differs
    public void Equals_WhenAnyComponentDiffers_IsFalse(int x, string y)
    {
        var a = new Pair(1, "a");
        var b = new Pair(x, y);

        a.Equals(b).Should().BeFalse();
        (a == b).Should().BeFalse();
        (a != b).Should().BeTrue();
    }

    [Fact]
    public void Equals_DifferentConcreteTypeWithSameComponents_IsFalse()
    {
        var a = new Pair(1, "a");
        var b = new OtherPair(1, "a");

        // Component values are identical, but a Pair is not an OtherPair.
        a.Equals(b).Should().BeFalse();
        a.Equals((object)b).Should().BeFalse();
    }

    [Fact]
    public void Equals_Null_IsFalse()
    {
        var a = new Pair(1, "a");
        Pair? nullPair = null;

        a.Equals(null).Should().BeFalse();
        (a == nullPair).Should().BeFalse();
        (a != nullPair).Should().BeTrue();
    }

    [Fact]
    public void Equals_BothNullViaOperators_IsTrue()
    {
        Pair? a = null;
        Pair? b = null;

        (a == b).Should().BeTrue();
        (a != b).Should().BeFalse();
    }

    [Fact]
    public void GetHashCode_EqualValues_AreEqual()
    {
        var a = new Pair(1, "a");
        var b = new Pair(1, "a");

        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void GetHashCode_DifferentValues_DifferInTypicalCases()
    {
        var a = new Pair(1, "a");
        var b = new Pair(2, "b");

        // Not a strict guarantee for all inputs, but these distinct components hash apart.
        a.GetHashCode().Should().NotBe(b.GetHashCode());
    }
}
