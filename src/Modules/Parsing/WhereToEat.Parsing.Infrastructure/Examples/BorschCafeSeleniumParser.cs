using WhereToEat.Parsing.Domain;
using WhereToEat.Parsing.Infrastructure.Strategies;
using WhereToEat.SharedKernel.Results;

namespace WhereToEat.Parsing.Infrastructure.Examples;

/// <summary>
/// A concrete <b>example restaurant parser of the Selenium kind</b> (invariant #4 — a strategy
/// registered by its own key). "Borsch Cafe" is a JS-rendered site, so this example delegates to the
/// shared <see cref="SeleniumParserStrategy"/> (which drives a headless browser through the
/// <c>IWebDriverFactory</c> seam) and only fixes the strategy <see cref="Key"/> the venue's
/// <c>SourceDescriptor</c> resolves to. It produces the same normalized <see cref="ParsedMenu"/>
/// contract every parser does. Adding another Selenium restaurant is the same shape: a new example
/// type + a fixture + a test class, with no engine change — which the e2e harness demonstrates.
/// </summary>
public sealed class BorschCafeSeleniumParser : IRestaurantParser
{
    /// <summary>The key this example registers under (the venue's source descriptor names it).</summary>
    public const string Key = "borsch-cafe";

    private readonly SeleniumParserStrategy _selenium;

    public BorschCafeSeleniumParser(SeleniumParserStrategy selenium)
    {
        ArgumentNullException.ThrowIfNull(selenium);
        _selenium = selenium;
    }

    /// <inheritdoc />
    public string StrategyKey => Key;

    /// <inheritdoc />
    public Task<Result<ParsedMenu>> ParseAsync(SourceDescriptor source, CancellationToken cancellationToken = default)
        => _selenium.ParseAsync(source, cancellationToken);
}
