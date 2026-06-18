using System.Globalization;
using WhereToEat.Analytics.Application;

namespace WhereToEat.Analytics.Infrastructure.Anonymization;

/// <summary>
/// The <see cref="ISaltProvider"/> implementation: it derives the current salt from the configured
/// master secret and the <b>current rotation window index</b> (the UTC clock divided by the window
/// length). The result is therefore <b>stable within a window</b> (every call in the same window sees
/// the same salt, so intra-window funnel math works) and <b>different across windows</b> (the index
/// changes, so the same user id hashes differently once the window rolls — not cross-window linkable).
/// The salt is never persisted; only the hash that mixes it is.
/// </summary>
public sealed class RotatingSaltProvider : ISaltProvider
{
    private readonly AnonymizerOptions _options;
    private readonly Func<DateTimeOffset> _clock;

    public RotatingSaltProvider(AnonymizerOptions options, Func<DateTimeOffset>? clock = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.SaltRotationWindow <= TimeSpan.Zero)
        {
            throw new ArgumentException("The salt rotation window must be positive.", nameof(options));
        }

        _options = options;
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    /// <inheritdoc />
    public string CurrentSalt
    {
        get
        {
            var windowTicks = _options.SaltRotationWindow.Ticks;
            var windowIndex = _clock().UtcDateTime.Ticks / windowTicks;

            // The salt is (master secret + window index): constant within a window, distinct across
            // windows. It is an input to the keyed hash, never stored on its own.
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{_options.MasterSecret}:{windowIndex}");
        }
    }
}
