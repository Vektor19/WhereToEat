/**
 * Wiring for the **active** map provider behind {@link MAP_PROVIDER} (§5.7, design decision #11).
 *
 * The selection is **config-driven, never hardcoded in a component**: with a Google Maps Embed
 * credential present (and the `'google-embed'` provider selected) the active port is the
 * {@link GoogleEmbedMapProvider}; with no credential — or when the provider is explicitly set to
 * `'deep-link'` — it degrades to the {@link DeepLinkMapProvider}. Either way the modal injects the
 * single {@link MAP_PROVIDER} port and never knows which implementation backs it (and the Embed
 * provider still degrades per-payload when a particular restaurant lacks an embeddable target).
 *
 * Exposed both as a reusable factory ({@link selectMapProvider}, unit-tested directly) and an
 * Angular provider ({@link provideMapProvider}, added to the app's root providers).
 */
import { inject, type EnvironmentProviders, type Provider } from '@angular/core';
import { makeEnvironmentProviders } from '@angular/core';

import { APP_CONFIG, type MapConfig } from '../config/app-config.token';
import { DeepLinkMapProvider } from './deep-link.provider';
import { GoogleEmbedMapProvider } from './google-embed.provider';
import { MAP_PROVIDER, type MapProvider } from './map-provider';

/**
 * Pick the provider implementation for the given map config: the Google Embed provider when the
 * `'google-embed'` provider is selected **and** an Embed credential is configured; the deep-link
 * provider otherwise (no credential, or an explicit `'deep-link'` selection). Pure and synchronous so
 * it is unit-testable without an injector.
 */
export function selectMapProvider(
  config: MapConfig,
  embed: GoogleEmbedMapProvider,
  deepLink: DeepLinkMapProvider,
): MapProvider {
  const useEmbed = config.provider === 'google-embed' && config.googleMapsEmbedKey.length > 0;
  return useEmbed ? embed : deepLink;
}

/**
 * Provide the active {@link MAP_PROVIDER} for the application. The factory resolves the concrete
 * implementations and the config from DI and delegates the choice to {@link selectMapProvider}.
 */
export function provideMapProvider(): EnvironmentProviders {
  const provider: Provider = {
    provide: MAP_PROVIDER,
    useFactory: (): MapProvider =>
      selectMapProvider(
        inject(APP_CONFIG).map,
        inject(GoogleEmbedMapProvider),
        inject(DeepLinkMapProvider),
      ),
  };
  return makeEnvironmentProviders([provider]);
}
