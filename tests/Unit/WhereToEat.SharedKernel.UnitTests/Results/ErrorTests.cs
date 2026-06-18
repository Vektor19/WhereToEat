using FluentAssertions;
using WhereToEat.SharedKernel.Results;
using Xunit;

namespace WhereToEat.SharedKernel.UnitTests.Results;

public sealed class ErrorTests
{
    [Fact]
    public void None_IsTheEmptySentinel()
    {
        Error.None.Code.Should().BeEmpty();
        Error.None.Message.Should().BeEmpty();
    }

    [Fact]
    public void Factories_SetTheExpectedType()
    {
        Error.Validation("c", "m").Type.Should().Be(ErrorType.Validation);
        Error.NotFound("c", "m").Type.Should().Be(ErrorType.NotFound);
        Error.Conflict("c", "m").Type.Should().Be(ErrorType.Conflict);
        Error.Failure("c", "m").Type.Should().Be(ErrorType.Failure);
    }

    [Fact]
    public void Equality_IsByValue()
    {
        var a = new Error("Code", "Message", ErrorType.Validation);
        var b = new Error("Code", "Message", ErrorType.Validation);
        var c = new Error("Code", "Message", ErrorType.NotFound);

        a.Should().Be(b);
        a.Should().NotBe(c);
        (a == b).Should().BeTrue();
    }
}
