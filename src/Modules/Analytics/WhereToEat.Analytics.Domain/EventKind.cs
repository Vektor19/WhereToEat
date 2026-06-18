namespace WhereToEat.Analytics.Domain;

/// <summary>
/// The kind of product-analytics event (§8.1) — the closed set of behaviours we log, knowingly and
/// anonymized (the actual ingest + anonymization pipeline is Step 11; this is the domain shape).
/// </summary>
public enum EventKind
{
    /// <summary>A restaurant card was shown in a result list.</summary>
    Impression = 0,

    /// <summary>A restaurant card was viewed.</summary>
    View = 1,

    /// <summary>A restaurant card was opened (details).</summary>
    CardOpen = 2,

    /// <summary>A target action fired (Look on map / phone / site / social link).</summary>
    Action = 3,

    /// <summary>A search/selection was built (categories/dishes + match mode).</summary>
    Search = 4,

    /// <summary>Filters were applied (price/rating/…).</summary>
    Filter = 5,

    /// <summary>A user gave a rating.</summary>
    RatingGiven = 6,

    /// <summary>A session boundary (start/heartbeat) — for retention/session math.</summary>
    Session = 7,
}
