import { TestBed } from '@angular/core/testing';

import { APP_CONFIG, type AppConfig } from '../config/app-config.token';
import type { MapPayloadDto } from '../domain';
import { GoogleEmbedMapProvider } from './google-embed.provider';

function config(embedKey: string): AppConfig {
  return {
    production: false,
    gatewayOrigin: '',
    apiPublicPrefix: '/api/public',
    apiAdminPrefix: '/api/admin',
    auth: { issuer: '', clientId: 'spa', redirectUri: '', scope: 'openid' },
    map: { provider: 'google-embed', googleMapsEmbedKey: embedKey },
    features: { analyticsEnabled: true, ratingSubmitEnabled: true, portalEnabled: true },
  };
}

function payload(over: Partial<MapPayloadDto> = {}): MapPayloadDto {
  return {
    restaurantId: 'r-1',
    hasMapData: true,
    latitude: null,
    longitude: null,
    placeId: null,
    mapsDeepLink: null,
    ...over,
  };
}

function createProvider(embedKey: string): GoogleEmbedMapProvider {
  TestBed.configureTestingModule({
    providers: [GoogleEmbedMapProvider, { provide: APP_CONFIG, useValue: config(embedKey) }],
  });
  return TestBed.inject(GoogleEmbedMapProvider);
}

describe('GoogleEmbedMapProvider', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('builds a live Place-mode embed URL from the config key + Place ID', () => {
    const view = createProvider('KEY-123').resolve(payload({ placeId: 'ChIJabc' }));
    expect(view.kind).toBe('embed');
    if (view.kind === 'embed') {
      expect(view.url).toContain('https://www.google.com/maps/embed/v1/place');
      expect(view.url).toContain('key=KEY-123');
      expect(view.url).toContain('place_id%3AChIJabc');
    }
  });

  it('builds a live view-mode embed URL from coordinates when no Place ID', () => {
    const view = createProvider('KEY-123').resolve(payload({ latitude: 50.45, longitude: 30.52 }));
    expect(view.kind).toBe('embed');
    if (view.kind === 'embed') {
      expect(view.url).toContain('https://www.google.com/maps/embed/v1/view');
      expect(view.url).toContain('center=50.45%2C30.52');
    }
  });

  it('degrades to the deep-link when no Embed credential is configured', () => {
    const view = createProvider('').resolve(payload({ placeId: 'ChIJabc' }));
    // No key → fall back to the deep-link provider's Place-ID query link, not a broken embed.
    expect(view.kind).toBe('deep-link');
    if (view.kind === 'deep-link') {
      expect(view.url).toContain('query_place_id=ChIJabc');
    }
  });

  it('degrades to the deep-link when the payload has no embeddable target', () => {
    // A credential is present but the payload has neither Place ID nor coordinates.
    const view = createProvider('KEY-123').resolve(
      payload({ mapsDeepLink: 'https://maps.app.goo.gl/x' }),
    );
    expect(view).toEqual({ kind: 'deep-link', url: 'https://maps.app.goo.gl/x' });
  });

  it('returns the no-map view for hasMapData=false even with a credential', () => {
    const view = createProvider('KEY-123').resolve(payload({ hasMapData: false }));
    expect(view).toEqual({ kind: 'none' });
  });
});
