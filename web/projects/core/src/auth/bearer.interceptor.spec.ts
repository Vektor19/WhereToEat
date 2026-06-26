import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';

import { ApiConfig } from '../config/api-config';
import { APP_CONFIG, type AppConfig } from '../config/app-config.token';

import { AuthService } from './auth.service';
import { bearerInterceptor } from './bearer.interceptor';

const TOKEN = 'access-token-abc';

function appConfig(): AppConfig {
  return {
    production: false,
    gatewayOrigin: '',
    apiPublicPrefix: '/api/public',
    apiAdminPrefix: '/api/admin',
    auth: { issuer: '', clientId: 'spa', redirectUri: '', scope: 'openid' },
    map: { provider: 'google-embed', googleMapsEmbedKey: '' },
    features: { analyticsEnabled: true, ratingSubmitEnabled: true, portalEnabled: true },
  };
}

/** A minimal AuthService stub exposing the one method the interceptor calls. */
function authStub(token: string): Pick<AuthService, 'accessToken'> {
  return { accessToken: () => token };
}

function setup(token = TOKEN): { http: HttpClient; ctrl: HttpTestingController } {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(withInterceptors([bearerInterceptor])),
      provideHttpClientTesting(),
      ApiConfig,
      { provide: APP_CONFIG, useValue: appConfig() },
      { provide: AuthService, useValue: authStub(token) },
    ],
  });
  return {
    http: TestBed.inject(HttpClient),
    ctrl: TestBed.inject(HttpTestingController),
  };
}

async function authHeaderFor(url: string, token = TOKEN): Promise<string | null> {
  const { http, ctrl } = setup(token);
  const call = firstValueFrom(http.get(url));
  const req = ctrl.expectOne(url);
  const header = req.request.headers.get('Authorization');
  req.flush({});
  await call;
  ctrl.verify();
  return header;
}

describe('bearerInterceptor', () => {
  afterEach(() => {
    TestBed.resetTestingModule();
  });

  it('attaches the bearer to an Admin-host call', async () => {
    expect(await authHeaderFor('/api/admin/menu-items/42')).toBe(`Bearer ${TOKEN}`);
  });

  it('attaches the bearer to the authenticated public /me call', async () => {
    expect(await authHeaderFor('/api/public/me')).toBe(`Bearer ${TOKEN}`);
  });

  it('attaches the bearer to the authenticated public rating-submit call', async () => {
    // Step 22's endpoint is POST /restaurants/{id}/ratings — the `/ratings` suffix rule matches it.
    expect(await authHeaderFor('/api/public/restaurants/99/ratings')).toBe(`Bearer ${TOKEN}`);
  });

  it('does NOT attach the bearer to the anonymous restaurant-details read (no /ratings suffix)', async () => {
    // The details GET shares the /restaurants/{id} stem but has no /ratings suffix → stays token-free.
    expect(await authHeaderFor('/api/public/restaurants/99')).toBeNull();
  });

  it('does NOT attach the bearer to anonymous public reads', async () => {
    expect(await authHeaderFor('/api/public/categories')).toBeNull();
    expect(await authHeaderFor('/api/public/restaurants/1')).toBeNull();
    expect(await authHeaderFor('/api/public/search/dishes?prefix=bo')).toBeNull();
    expect(await authHeaderFor('/api/public/recommend')).toBeNull();
    expect(await authHeaderFor('/api/public/map/1')).toBeNull();
    expect(await authHeaderFor('/api/public/analytics/events')).toBeNull();
    expect(await authHeaderFor('/api/public/health')).toBeNull();
  });

  it('does NOT attach the bearer to a non-API path', async () => {
    expect(await authHeaderFor('/assets/i18n/uk.json')).toBeNull();
  });

  it('does NOT attach the bearer to a cross-origin URL even on an admin-looking path', async () => {
    expect(await authHeaderFor('https://evil.example.com/api/admin/menu-items/1')).toBeNull();
  });

  it('issues the request without a header when no token is held', async () => {
    expect(await authHeaderFor('/api/admin/menu-items/42', '')).toBeNull();
  });
});
