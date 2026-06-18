using Microsoft.Extensions.Logging;
using OpenQA.Selenium;
using WhereToEat.Parsing.Domain;
using WhereToEat.SharedKernel.Results;

namespace WhereToEat.Parsing.Infrastructure.Strategies;

/// <summary>
/// The Selenium-based parser strategy (invariant #4) for <b>JS-rendered</b> sites: it drives a real
/// (headless) browser created by the <see cref="IWebDriverFactory"/> seam, navigates to the source
/// URL, lets the page render, and reads the menu through the shared <see cref="MenuHtmlConvention"/>
/// so its output is the <b>same</b> normalized <see cref="ParsedMenu"/> contract the AngleSharp
/// strategy produces. The driver factory is a seam so the e2e harness injects a headless driver
/// pointed at a locally-served pinned fixture (CI-stable, never the live site).
///
/// Compliance gates run in the Application pipeline before this is invoked (invariant #9); this
/// strategy assumes it is already cleared to fetch. A WebDriver/parse error is returned as a failure
/// <see cref="Result{T}"/> (and the driver is always quit) rather than thrown, so a single bad source
/// does not abort a batch.
/// </summary>
public sealed partial class SeleniumParserStrategy : IRestaurantParser
{
    /// <summary>The key this strategy registers under.</summary>
    public const string Key = "selenium";

    private readonly IWebDriverFactory _driverFactory;
    private readonly ILogger<SeleniumParserStrategy> _logger;

    public SeleniumParserStrategy(IWebDriverFactory driverFactory, ILogger<SeleniumParserStrategy> logger)
    {
        ArgumentNullException.ThrowIfNull(driverFactory);
        ArgumentNullException.ThrowIfNull(logger);
        _driverFactory = driverFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public string StrategyKey => Key;

    /// <inheritdoc />
    public Task<Result<ParsedMenu>> ParseAsync(SourceDescriptor source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        IWebDriver? driver = null;
        try
        {
            driver = _driverFactory.Create();
            driver.Navigate().GoToUrl(source.Url);

            var root = driver.FindElement(By.CssSelector(MenuHtmlConvention.RestaurantRootSelector));
            var currency = root.GetAttribute("data-currency") ?? "UAH";

            var facts = new ParsedRestaurantFacts(
                root.GetAttribute("data-name") ?? source.RestaurantName,
                root.GetAttribute("data-address") ?? string.Empty,
                root.GetAttribute("data-city"),
                ReadContactLinks(driver));

            var dishes = ReadDishes(driver, currency);

            return Task.FromResult(Result.Success(new ParsedMenu(facts, dishes)));
        }
        catch (WebDriverException ex)
        {
            LogFetchFailed(source.Url, ex.Message);
            return Task.FromResult(Result.Failure<ParsedMenu>(
                Error.Failure("Parse.Selenium.Fetch", $"Selenium could not fetch/parse '{source.Url}': {ex.Message}")));
        }
        finally
        {
            driver?.Quit();
            driver?.Dispose();
        }
    }

    private static List<ParsedContactLink> ReadContactLinks(IWebDriver driver)
    {
        var links = new List<ParsedContactLink>();
        foreach (var element in driver.FindElements(By.CssSelector(MenuHtmlConvention.ContactLinkSelector)))
        {
            var url = element.GetAttribute("href") ?? element.GetAttribute("data-url");
            if (string.IsNullOrWhiteSpace(url))
            {
                continue;
            }

            links.Add(MenuHtmlConvention.ToContactLink(
                element.GetAttribute("data-contact"),
                url,
                element.Text));
        }

        return links;
    }

    private static List<ParsedDish> ReadDishes(IWebDriver driver, string currency)
    {
        var dishes = new List<ParsedDish>();
        foreach (var row in driver.FindElements(By.CssSelector(MenuHtmlConvention.DishSelector)))
        {
            var dish = MenuHtmlConvention.ToDish(
                ReadChildText(row, ".name"),
                ReadChildText(row, ".price"),
                ReadChildText(row, ".weight"),
                row.GetAttribute("data-category"),
                currency);

            if (dish is not null)
            {
                dishes.Add(dish);
            }
        }

        return dishes;
    }

    // Reads the text of a descendant by CSS selector, or null when the element is absent.
    private static string? ReadChildText(IWebElement row, string cssSelector)
    {
        var matches = row.FindElements(By.CssSelector(cssSelector));
        return matches.Count == 0 ? null : matches[0].Text;
    }

    [LoggerMessage(EventId = 1, Level = Microsoft.Extensions.Logging.LogLevel.Warning,
        Message = "Selenium fetch/parse failed for {Url}: {Reason}.")]
    private partial void LogFetchFailed(Uri url, string reason);
}
