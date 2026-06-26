/**
 * Barrel for the `core` map layer — the **swappable map-provider interface** (§5.7, decision #11) and
 * its two implementations: the live Google Maps **Embed** provider (credential from config) and the
 * **deep-link** degradation. Plus the config-driven selection that binds the active {@link MAP_PROVIDER}
 * port. The map source stays a clean interface the React Native app re-implements with its own
 * provider. Re-exported through the library public-api so the `app` consumes it through the one boundary.
 */
export * from './map-provider';
export * from './deep-link.provider';
export * from './google-embed.provider';
export * from './map-provider.selection';
