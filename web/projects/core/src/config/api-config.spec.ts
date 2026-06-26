import { TestBed } from '@angular/core/testing';

import { ApiConfig } from './api-config';
import { APP_CONFIG, type AppConfig } from './app-config.token';

function configWith(overrides: Partial<AppConfig> = {}): AppConfig {
  return {
    production: false,
    gatewayOrigin: '',
    apiPublicPrefix: '/api/public',
    apiAdminPrefix: '/api/admin',
    auth: { issuer: '', clientId: 'spa', redirectUri: '', scope: 'openid' },
    map: { provider: 'google-embed', googleMapsEmbedKey: '' },
    features: {
      analyticsEnabled: true,
      ratingSubmitEnabled: true,
      portalEnabled: true,
    },
    ...overrides,
  };
}

function createApiConfig(config: AppConfig): ApiConfig {
  TestBed.configureTestingModule({
    providers: [ApiConfig, { provide: APP_CONFIG, useValue: config }],
  });
  return TestBed.inject(ApiConfig);
}

describe('ApiConfig', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('exposes the configured logical-host prefixes', () => {
    const api = createApiConfig(configWith());

    expect(api.publicPrefix).toBe('/api/public');
    expect(api.adminPrefix).toBe('/api/admin');
  });

  it('composes a public URL from the public prefix and endpoint path', () => {
    const api = createApiConfig(configWith());

    expect(api.publicUrl('/categories')).toBe('/api/public/categories');
    expect(api.publicUrl('/recommend')).toBe('/api/public/recommend');
  });

  it('composes an admin URL from the admin prefix and endpoint path', () => {
    const api = createApiConfig(configWith());

    // Admin endpoints already carry their own `/admin/...` segment.
    expect(api.adminUrl('/admin/menu-items/abc')).toBe('/api/admin/admin/menu-items/abc');
  });

  it('never emits an absolute cross-origin host with the default empty origin', () => {
    const api = createApiConfig(configWith());

    // Pin the exact composed admin path so a compose regression that keeps the relative
    // format but changes the prefix is caught (not just the format-only checks below).
    expect(api.adminUrl('/admin/venues/1/verified')).toBe('/api/admin/admin/venues/1/verified');

    for (const url of [api.publicUrl('/categories'), api.adminUrl('/admin/venues/1/verified')]) {
      // A relative path the dev proxy / gateway forwards — no scheme, no host.
      expect(url.startsWith('/')).toBe(true);
      expect(/^https?:\/\//.test(url)).toBe(false);
    }
  });

  it('prepends a configured remote gateway origin when one is set', () => {
    const api = createApiConfig(configWith({ gatewayOrigin: 'https://gw.example.com' }));

    expect(api.publicUrl('/categories')).toBe('https://gw.example.com/api/public/categories');
  });

  it('normalizes slashes so prefixes/paths never double or drop separators', () => {
    const api = createApiConfig(
      configWith({ gatewayOrigin: 'https://gw.example.com/', apiPublicPrefix: '/api/public/' }),
    );

    expect(api.publicUrl('categories')).toBe('https://gw.example.com/api/public/categories');
  });

  it('reflects a prefix change from config without any code change', () => {
    const api = createApiConfig(configWith({ apiPublicPrefix: '/gateway/v2/public' }));

    expect(api.publicUrl('/categories')).toBe('/gateway/v2/public/categories');
  });
});
