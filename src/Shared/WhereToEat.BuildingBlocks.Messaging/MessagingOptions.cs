namespace WhereToEat.BuildingBlocks.Messaging;

/// <summary>
/// Binds the <c>Messaging</c> configuration section. <see cref="Transport"/> selects the MassTransit
/// transport <b>by config</b>: <c>InMemory</c> (the default now — no broker dependency) or
/// <c>RabbitMq</c> (production-ready; connects to the configured broker). Switching transports is a
/// configuration change only — the bus registration, the consumers, and the published contracts stay
/// identical (CLAUDE.md invariant #4: pluggable infrastructure behind clean seams).
/// </summary>
public sealed class MessagingOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "Messaging";

    /// <summary>The selected transport (<c>InMemory</c> or <c>RabbitMq</c>). Defaults to in-memory.</summary>
    public string Transport { get; set; } = MessagingTransport.InMemory;

    /// <summary>The RabbitMQ broker host (used only when <see cref="Transport"/> is <c>RabbitMq</c>).</summary>
    public string Host { get; set; } = "localhost";

    /// <summary>The RabbitMQ virtual host.</summary>
    public string VirtualHost { get; set; } = "/";

    /// <summary>The RabbitMQ username.</summary>
    public string Username { get; set; } = "guest";

    /// <summary>The RabbitMQ password.</summary>
    public string Password { get; set; } = "guest";
}

/// <summary>The supported MassTransit transport values for <see cref="MessagingOptions.Transport"/>.</summary>
public static class MessagingTransport
{
    /// <summary>The in-process transport (no broker) — the default for local/dev now.</summary>
    public const string InMemory = "InMemory";

    /// <summary>The RabbitMQ transport (production-ready; connects to the configured broker).</summary>
    public const string RabbitMq = "RabbitMq";
}
