using Microsoft.Extensions.Options;
using WhereToEat.Parsing.Domain;

namespace WhereToEat.Worker.Scheduling;

/// <summary>
/// Options for the configuration-driven set of first-party parse sources (bound from the
/// <c>Worker:ParseSources</c> appsettings array). Each entry is a first-party restaurant site the
/// weekly job parses; the compliance gates inside the Step 9 handler still vet every URL before fetch.
/// </summary>
public sealed class ParseSourceOptions
{
    /// <summary>The appsettings section the source list binds to.</summary>
    public const string SectionName = "Worker:ParseSources";

    /// <summary>The configured first-party sources (name + URL + parser strategy key).</summary>
    public IList<ParseSourceEntry> Sources { get; } = new List<ParseSourceEntry>();
}

/// <summary>One configured first-party parse source.</summary>
public sealed class ParseSourceEntry
{
    /// <summary>The restaurant the source represents.</summary>
    public string RestaurantName { get; set; } = string.Empty;

    /// <summary>The first-party URL to fetch (must be absolute).</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>The keyed parser strategy that handles this source (e.g. "selenium", "anglesharp").</summary>
    public string StrategyKey { get; set; } = string.Empty;
}

/// <summary>
/// The default <see cref="IParseSourceProvider"/>: maps the configured <see cref="ParseSourceEntry"/>
/// list into <see cref="SourceDescriptor"/>s. Entries with a missing/invalid absolute URL are skipped
/// (a misconfigured source must not break the whole weekly run); a well-formed entry becomes one
/// descriptor the Step 9 pipeline processes.
/// </summary>
public sealed class ConfigurationParseSourceProvider : IParseSourceProvider
{
    private readonly ParseSourceOptions _options;

    public ConfigurationParseSourceProvider(IOptions<ParseSourceOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
    }

    /// <inheritdoc />
    public IReadOnlyList<SourceDescriptor> GetSources()
    {
        var descriptors = new List<SourceDescriptor>(_options.Sources.Count);
        foreach (var entry in _options.Sources)
        {
            if (string.IsNullOrWhiteSpace(entry.Url)
                || !Uri.TryCreate(entry.Url, UriKind.Absolute, out var url)
                || string.IsNullOrWhiteSpace(entry.StrategyKey)
                || string.IsNullOrWhiteSpace(entry.RestaurantName))
            {
                continue;
            }

            descriptors.Add(new SourceDescriptor(entry.RestaurantName, url, entry.StrategyKey));
        }

        return descriptors;
    }
}
