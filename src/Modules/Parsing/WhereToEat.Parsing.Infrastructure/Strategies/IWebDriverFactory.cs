using OpenQA.Selenium;

namespace WhereToEat.Parsing.Infrastructure.Strategies;

/// <summary>
/// Creates the Selenium <see cref="IWebDriver"/> the <see cref="SeleniumParserStrategy"/> drives. A
/// seam (not a hard <c>new ChromeDriver()</c>) so the e2e harness can inject a <b>headless</b> driver
/// pointed at a locally-served pinned fixture — CI-stable and never the live site — while production
/// wires a real headless browser. The caller owns the returned driver's lifetime and quits it.
/// </summary>
public interface IWebDriverFactory
{
    /// <summary>Creates a new, ready-to-use WebDriver instance (the caller disposes/quits it).</summary>
    IWebDriver Create();
}
