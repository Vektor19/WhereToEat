namespace WhereToEat.Analytics.Application;

/// <summary>
/// Supplies the <b>rotating salt</b> the anonymizer mixes into a user/session id before hashing
/// (§8.1 / invariant #11). The salt is constant within a time window and changes between windows, so
/// the same raw id hashes <b>stably within a window</b> (intra-window funnel math works) yet
/// <b>differently across windows</b> (not cross-window linkable — the raw id is unrecoverable and a
/// stored hash de-links once its window has rolled over).
/// </summary>
public interface ISaltProvider
{
    /// <summary>
    /// The current window's salt. Stable for every call within the same rotation window; a different
    /// value once the window rolls over.
    /// </summary>
    string CurrentSalt { get; }
}
