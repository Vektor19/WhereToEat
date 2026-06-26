import { TestBed } from '@angular/core/testing';

import { APP_CONFIG, type AppConfig, type MapConfig } from '../config/app-config.token';
import { DeepLinkMapProvider } from './deep-link.provider';
import { GoogleEmbedMapProvider } from './google-embed.provider';
import { MAP_PROVIDER } from './map-provider';
import { provideMapProvider, selectMapProvider } from './map-provider.selection';

function appConfig(map: MapConfig): AppConfig {
  return {
    production: false,
    gatewayOrigin: '',
    apiPublicPrefix: '/api/public',
    apiAdminPrefix: '/api/admin',
    auth: { issuer: '', clientId: 'spa', redirectUri: '', scope: 'openid' },
    map,
    features: { analyticsEnabled: true, ratingSubmitEnabled: true, portalEnabled: true },
  };
}

describe('selectMapProvider', () => {
  const embed = { name: 'embed' } as unknown as GoogleEmbedMapProvider;
  const deepLink = { name: 'deep-link' } as unknown as DeepLinkMapProvider;

  it('chooses the Embed provider when google-embed is selected and a credential is present', () => {
    const chosen = selectMapProvider(
      { provider: 'google-embed', googleMapsEmbedKey: 'KEY' },
      embed,
      deepLink,
    );
    expect(chosen).toBe(embed);
  });

  it('chooses the deep-link provider when no credential is present', () => {
    const chosen = selectMapProvider(
      { provider: 'google-embed', googleMapsEmbedKey: '' },
      embed,
      deepLink,
    );
    expect(chosen).toBe(deepLink);
  });

  it('chooses the deep-link provider when it is explicitly selected', () => {
    const chosen = selectMapProvider(
      { provider: 'deep-link', googleMapsEmbedKey: 'KEY' },
      embed,
      deepLink,
    );
    expect(chosen).toBe(deepLink);
  });
});

describe('provideMapProvider', () => {
  afterEach(() => TestBed.resetTestingModule());

  function resolve(map: MapConfig): unknown {
    TestBed.configureTestingModule({
      providers: [provideMapProvider(), { provide: APP_CONFIG, useValue: appConfig(map) }],
    });
    return TestBed.inject(MAP_PROVIDER);
  }

  it('binds MAP_PROVIDER to the Embed provider when a credential is configured', () => {
    expect(resolve({ provider: 'google-embed', googleMapsEmbedKey: 'KEY' })).toBeInstanceOf(
      GoogleEmbedMapProvider,
    );
  });

  it('binds MAP_PROVIDER to the deep-link provider when no credential is configured', () => {
    expect(resolve({ provider: 'google-embed', googleMapsEmbedKey: '' })).toBeInstanceOf(
      DeepLinkMapProvider,
    );
  });
});
