using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

namespace WhereToEat.Parsing.Infrastructure.Strategies;

/// <summary>
/// The production <see cref="IWebDriverFactory"/>: creates a <b>headless</b> Chrome driver (Selenium
/// Manager auto-resolves the matching driver binary, so no manual driver provisioning). Headless is
/// mandatory for an unattended worker (Step 12). The e2e harness substitutes its own factory to point
/// a headless driver at a locally-served pinned fixture, so this concrete factory is never exercised
/// in CI — it is the runtime wiring only.
/// </summary>
public sealed class ChromeWebDriverFactory : IWebDriverFactory
{
    /// <inheritdoc />
    public IWebDriver Create()
    {
        var options = new ChromeOptions();
        options.AddArgument("--headless=new");
        options.AddArgument("--no-sandbox");
        options.AddArgument("--disable-dev-shm-usage");
        options.AddArgument("--disable-gpu");
        return new ChromeDriver(options);
    }
}
