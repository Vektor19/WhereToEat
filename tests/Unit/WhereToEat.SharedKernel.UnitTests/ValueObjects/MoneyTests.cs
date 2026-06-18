using FluentAssertions;
using WhereToEat.SharedKernel.Results;
using WhereToEat.SharedKernel.ValueObjects;
using Xunit;

namespace WhereToEat.SharedKernel.UnitTests.ValueObjects;

public sealed class MoneyTests
{
    [Fact]
    public void Create_WithValidAmountAndCurrency_Succeeds_AndNormalizesCurrency()
    {
        var result = Money.Create(149.50m, "uah");

        result.IsSuccess.Should().BeTrue();
        result.Value.Amount.Should().Be(149.50m);
        result.Value.Currency.Should().Be("UAH");
    }

    [Fact]
    public void Create_WithNegativeAmount_Fails()
    {
        var result = Money.Create(-0.01m, "UAH");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Money.NegativeAmount");
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Theory]
    [InlineData("US")]      // too short
    [InlineData("USDD")]    // too long
    [InlineData("US1")]     // not all letters
    [InlineData("")]        // empty
    [InlineData("   ")]     // whitespace
    [InlineData("éàñ")]     // 3 non-ASCII Latin letters — ISO 4217 is ASCII A–Z only
    [InlineData("ГРН")]     // 3 Cyrillic letters — passes length+IsLetter, rejected by ASCII guard
    [InlineData("ＵＡＨ")]     // 3 fullwidth Latin letters — Unicode letters, not ASCII A–Z
    public void Create_WithMalformedCurrency_Fails(string currency)
    {
        var result = Money.Create(10m, currency);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Money.InvalidCurrency");
    }

    [Fact]
    public void Equality_IsByValue()
    {
        var a = Money.Create(10m, "USD").Value;
        var b = Money.Create(10m, "USD").Value;
        var differentAmount = Money.Create(11m, "USD").Value;
        var differentCurrency = Money.Create(10m, "EUR").Value;

        a.Should().Be(b);
        (a == b).Should().BeTrue();
        a.Should().NotBe(differentAmount);
        a.Should().NotBe(differentCurrency);
    }

    [Fact]
    public void Add_SameCurrency_Sums()
    {
        var a = Money.Create(10m, "UAH").Value;
        var b = Money.Create(5.25m, "UAH").Value;

        (a + b).Amount.Should().Be(15.25m);
        (a + b).Currency.Should().Be("UAH");
    }

    [Fact]
    public void Add_DifferentCurrencies_Throws()
    {
        var a = Money.Create(10m, "UAH").Value;
        var b = Money.Create(5m, "USD").Value;

        var act = () => _ = a + b;

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*different currencies*");
    }

    [Fact]
    public void Subtract_DifferentCurrencies_Throws()
    {
        var a = Money.Create(10m, "UAH").Value;
        var b = Money.Create(5m, "USD").Value;

        var act = () => _ = a - b;

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Subtract_ProducingNegative_Throws()
    {
        var a = Money.Create(5m, "UAH").Value;
        var b = Money.Create(10m, "UAH").Value;

        var act = () => _ = a - b;

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*negative*");
    }

    [Fact]
    public void Zero_IsZeroOfCurrency()
    {
        var zero = Money.Zero("UAH").Value;

        zero.Amount.Should().Be(0m);
        zero.Currency.Should().Be("UAH");
    }
}
