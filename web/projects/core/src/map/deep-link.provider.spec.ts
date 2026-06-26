import { TestBed } from '@angular/core/testing';

import type { MapPayloadDto } from '../domain';
import { DeepLinkMapProvider } from './deep-link.provider';

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

describe('DeepLinkMapProvider', () => {
  let provider: DeepLinkMapProvider;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [DeepLinkMapProvider] });
    provider = TestBed.inject(DeepLinkMapProvider);
  });

  afterEach(() => TestBed.resetTestingModule());

  it('prefers the backend-supplied deep-link verbatim', () => {
    const view = provider.resolve(
      payload({
        mapsDeepLink: 'https://maps.app.goo.gl/abc',
        placeId: 'PID',
        latitude: 1,
        longitude: 2,
      }),
    );
    expect(view).toEqual({ kind: 'deep-link', url: 'https://maps.app.goo.gl/abc' });
  });

  it('builds a Place-ID query link when only a Place ID is present', () => {
    const view = provider.resolve(payload({ placeId: 'PID 1' }));
    expect(view.kind).toBe('deep-link');
    if (view.kind === 'deep-link') {
      expect(view.url).toContain('query_place_id=PID%201');
    }
  });

  it('builds a coordinate query link when only coordinates are present', () => {
    const view = provider.resolve(payload({ latitude: 50.45, longitude: 30.52 }));
    expect(view.kind).toBe('deep-link');
    if (view.kind === 'deep-link') {
      expect(view.url).toContain('query=50.45%2C30.52');
    }
  });

  it('returns the no-map view when hasMapData is false (invariant #7 — no stored coords)', () => {
    const view = provider.resolve(payload({ hasMapData: false, placeId: 'PID' }));
    expect(view).toEqual({ kind: 'none' });
  });

  it('returns the no-map view when no usable field is present', () => {
    const view = provider.resolve(payload());
    expect(view).toEqual({ kind: 'none' });
  });
});
