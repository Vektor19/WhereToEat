using WhereToEat.Parsing.Application;
using WhereToEat.Parsing.Domain;
using WhereToEat.SharedKernel.Results;

namespace WhereToEat.AdminApi.Integration.Parsing;

/// <summary>
/// A stub <see cref="IRestaurantParser"/> + <see cref="IParserStrategySelector"/> for the end-to-end
/// protection test: it returns a fixed <see cref="ParsedMenu"/> (no browser, no HTML fetch) so the
/// test drives the real Step 9 <c>RunParseCommandHandler</c> through its normalize + admin-protected
/// persist steps deterministically. The fixed menu stands in for what a real strategy would extract
/// from a pinned fixture — the protection assertion is about the persist gate, not the fetch.
/// </summary>
internal sealed class StubParserStrategy : IRestaurantParser, IParserStrategySelector
{
    public const string Key = "stub";

    private readonly ParsedMenu _menu;

    public StubParserStrategy(ParsedMenu menu) => _menu = menu;

    /// <inheritdoc />
    public string StrategyKey => Key;

    /// <inheritdoc />
    public Task<Result<ParsedMenu>> ParseAsync(SourceDescriptor source, CancellationToken cancellationToken = default)
        => Task.FromResult(Result.Success(_menu));

    /// <inheritdoc />
    public Result<IRestaurantParser> Resolve(string strategyKey)
        => strategyKey == Key
            ? Result.Success<IRestaurantParser>(this)
            : Result.Failure<IRestaurantParser>(Error.NotFound("Parser.UnknownStrategy", strategyKey));
}
