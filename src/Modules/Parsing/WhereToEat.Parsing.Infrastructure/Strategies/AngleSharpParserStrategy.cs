using AngleSharp;
using AngleSharp.Dom;
using Microsoft.Extensions.Logging;
using WhereToEat.Parsing.Domain;
using WhereToEat.SharedKernel.Results;

namespace WhereToEat.Parsing.Infrastructure.Strategies;

/// <summary>
/// The library-based parser strategy (invariant #4) for <b>static HTML</b> sites: it fetches the page
/// with AngleSharp's in-process loader (no browser) and reads the menu through the shared
/// <see cref="MenuHtmlConvention"/>, so its output is the <b>same</b> normalized <see cref="ParsedMenu"/>
/// contract the Selenium strategy produces — which the integration test asserts. AngleSharp resolves
/// <c>file://</c> and <c>http(s)://</c> URLs, so the integration test can point it at a pinned local
/// fixture (CI-stable, never the live site).
///
/// Compliance gates run before this is invoked (invariant #9). A fetch/parse error is returned as a
/// failure <see cref="Result{T}"/> rather than thrown.
/// </summary>
public sealed partial class AngleSharpParserStrategy : IRestaurantParser
{
    /// <summary>The key this strategy registers under.</summary>
    public const string Key = "anglesharp";

    private readonly ILogger<AngleSharpParserStrategy> _logger;

    public AngleSharpParserStrategy(ILogger<AngleSharpParserStrategy> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc />
    public string StrategyKey => Key;

    /// <inheritdoc />
    public async Task<Result<ParsedMenu>> ParseAsync(SourceDescriptor source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        try
        {
            var config = Configuration.Default.WithDefaultLoader();
            using var context = BrowsingContext.New(config);

            // For a local file (the pinned test fixtures) read the markup and open it directly — the
            // default network loader does not handle file:// URLs portably. http(s) sources still go
            // through the loader. Either way the parsing of the loaded DOM is identical.
            using var document = source.Url.IsFile
                ? await context.OpenAsync(
                    req => req.Content(File.ReadAllText(source.Url.LocalPath)), cancellationToken).ConfigureAwait(false)
                : await context.OpenAsync(source.Url.AbsoluteUri, cancellationToken).ConfigureAwait(false);

            var root = document.QuerySelector(MenuHtmlConvention.RestaurantRootSelector);
            if (root is null)
            {
                LogFetchFailed(source.Url, "no restaurant-facts root element");
                return Result.Failure<ParsedMenu>(
                    Error.Failure("Parse.AngleSharp.NoRoot", $"No menu root found at '{source.Url}'."));
            }

            var currency = root.GetAttribute("data-currency") ?? "UAH";

            var facts = new ParsedRestaurantFacts(
                root.GetAttribute("data-name") ?? source.RestaurantName,
                root.GetAttribute("data-address") ?? string.Empty,
                root.GetAttribute("data-city"),
                ReadContactLinks(document));

            var dishes = ReadDishes(document, currency);

            return Result.Success(new ParsedMenu(facts, dishes));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogFetchFailed(source.Url, ex.Message);
            return Result.Failure<ParsedMenu>(
                Error.Failure("Parse.AngleSharp.Fetch", $"AngleSharp could not fetch/parse '{source.Url}': {ex.Message}"));
        }
    }

    private static List<ParsedContactLink> ReadContactLinks(IDocument document)
    {
        var links = new List<ParsedContactLink>();
        foreach (var element in document.QuerySelectorAll(MenuHtmlConvention.ContactLinkSelector))
        {
            var url = element.GetAttribute("href") ?? element.GetAttribute("data-url");
            if (string.IsNullOrWhiteSpace(url))
            {
                continue;
            }

            links.Add(MenuHtmlConvention.ToContactLink(
                element.GetAttribute("data-contact"),
                url,
                element.TextContent));
        }

        return links;
    }

    private static List<ParsedDish> ReadDishes(IDocument document, string currency)
    {
        var dishes = new List<ParsedDish>();
        foreach (var row in document.QuerySelectorAll(MenuHtmlConvention.DishSelector))
        {
            var dish = MenuHtmlConvention.ToDish(
                row.QuerySelector(".name")?.TextContent,
                row.QuerySelector(".price")?.TextContent,
                row.QuerySelector(".weight")?.TextContent,
                row.GetAttribute("data-category"),
                currency);

            if (dish is not null)
            {
                dishes.Add(dish);
            }
        }

        return dishes;
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning,
        Message = "AngleSharp fetch/parse failed for {Url}: {Reason}.")]
    private partial void LogFetchFailed(Uri url, string reason);
}
