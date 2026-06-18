using WhereToEat.Parsing.Domain;
using WhereToEat.Parsing.Infrastructure.Strategies;
using WhereToEat.SharedKernel.Results;

namespace WhereToEat.Parsing.Infrastructure.Examples;

/// <summary>
/// A concrete <b>example restaurant parser of the library kind</b> (invariant #4 — a strategy
/// registered by its own key). "Pizza House" serves static HTML, so this example delegates to the
/// shared <see cref="AngleSharpParserStrategy"/> (in-process, no browser) and only fixes the strategy
/// <see cref="Key"/> the venue's <c>SourceDescriptor</c> resolves to. It produces the <b>same</b>
/// normalized <see cref="ParsedMenu"/> contract the Selenium example does — which the integration test
/// asserts. Adding another static-HTML restaurant is a new example type + a fixture + a test class,
/// with no engine change.
/// </summary>
public sealed class PizzaHouseAngleSharpParser : IRestaurantParser
{
    /// <summary>The key this example registers under (the venue's source descriptor names it).</summary>
    public const string Key = "pizza-house";

    private readonly AngleSharpParserStrategy _angleSharp;

    public PizzaHouseAngleSharpParser(AngleSharpParserStrategy angleSharp)
    {
        ArgumentNullException.ThrowIfNull(angleSharp);
        _angleSharp = angleSharp;
    }

    /// <inheritdoc />
    public string StrategyKey => Key;

    /// <inheritdoc />
    public Task<Result<ParsedMenu>> ParseAsync(SourceDescriptor source, CancellationToken cancellationToken = default)
        => _angleSharp.ParseAsync(source, cancellationToken);
}
