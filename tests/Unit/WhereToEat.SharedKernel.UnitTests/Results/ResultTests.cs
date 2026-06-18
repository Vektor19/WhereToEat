using FluentAssertions;
using WhereToEat.SharedKernel.Results;
using Xunit;

namespace WhereToEat.SharedKernel.UnitTests.Results;

public sealed class ResultTests
{
    [Fact]
    public void Success_NonGeneric_IsSuccessAndCarriesNoError()
    {
        var result = Result.Success();

        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Error.Should().Be(Error.None);
    }

    [Fact]
    public void Failure_NonGeneric_CarriesTheError()
    {
        var error = Error.NotFound("Restaurant.NotFound", "No such restaurant.");

        var result = Result.Failure(error);

        result.IsSuccess.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(error);
    }

    [Fact]
    public void SuccessOfT_ExposesValue()
    {
        var result = Result.Success(42);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void FailureOfT_CarriesError_AndAccessingValueThrows()
    {
        var error = Error.Validation("Code", "bad");

        var result = Result.Failure<int>(error);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(error);

        var access = () => _ = result.Value;
        access.Should().Throw<InvalidOperationException>()
            .WithMessage("*failed result*");
    }

    [Fact]
    public void Failure_WithNoError_Throws()
    {
        // Internal invariant: a failure must carry a real error — Error.None is rejected.
        var act = () => Result.Failure(Error.None);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ImplicitConversion_FromValue_ProducesSuccess()
    {
        Result<string> result = "hello";

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("hello");
    }

    [Fact]
    public void ImplicitConversion_FromError_ProducesFailure()
    {
        var error = Error.Failure("X", "y");

        Result<string> result = error;

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(error);
    }

    [Fact]
    public void Map_OnSuccess_ProjectsValue()
    {
        var result = Result.Success(10).Map(x => x * 2);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(20);
    }

    [Fact]
    public void Map_OnFailure_PropagatesError()
    {
        var error = Error.Validation("C", "m");

        var result = Result.Failure<int>(error).Map(x => x * 2);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(error);
    }

    [Fact]
    public void Bind_ChainsSuccessAndShortCircuitsFailure()
    {
        var chained = Result.Success(4).Bind(x => Result.Success(x + 1));
        chained.Value.Should().Be(5);

        var error = Error.Failure("C", "m");
        var shortCircuited = Result.Failure<int>(error).Bind(x => Result.Success(x + 1));
        shortCircuited.IsFailure.Should().BeTrue();
        shortCircuited.Error.Should().Be(error);
    }

    [Fact]
    public void Match_FoldsBothBranches()
    {
        Result.Success(7).Match(v => $"ok:{v}", e => $"err:{e.Code}").Should().Be("ok:7");
        Result.Failure<int>(Error.Failure("E", "m"))
            .Match(v => $"ok:{v}", e => $"err:{e.Code}")
            .Should().Be("err:E");
    }
}
