using WhereToEat.SharedKernel.Results;

namespace WhereToEat.Parsing.Domain;

/// <summary>
/// The parser strategy abstraction (invariant #4 — pluggable): turns a <see cref="SourceDescriptor"/>
/// into the normalized <see cref="ParsedMenu"/> contract. Concrete strategies (Selenium for
/// JS-rendered sites, AngleSharp for static HTML, a future Puppeteer locator) all implement this one
/// interface and are resolved <b>by key</b> (<see cref="StrategyKey"/>) so adding a restaurant is
/// "add a strategy + fixture + test", with no engine change.
///
/// A fetch/parse failure surfaces as a failure <see cref="Result{T}"/> rather than an exception so the
/// run pipeline can log-and-skip a single source without aborting a batch. Compliance gates
/// (robots.txt, rate-limit, first-party allow-list) run in the Application pipeline <b>before</b> a
/// parser is ever invoked — a strategy assumes it is already cleared to fetch (invariant #9).
/// </summary>
public interface IRestaurantParser
{
    /// <summary>
    /// The key this strategy registers under (e.g. <c>"selenium"</c>, <c>"anglesharp"</c>). The
    /// strategy selector resolves a <see cref="SourceDescriptor.StrategyKey"/> to the matching parser.
    /// </summary>
    string StrategyKey { get; }

    /// <summary>
    /// Fetches and parses the source into the normalized <see cref="ParsedMenu"/> contract. Returns a
    /// failure <see cref="Result{T}"/> on a fetch/parse error.
    /// </summary>
    Task<Result<ParsedMenu>> ParseAsync(SourceDescriptor source, CancellationToken cancellationToken = default);
}
