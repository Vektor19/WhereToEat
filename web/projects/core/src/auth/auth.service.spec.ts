import { TestBed } from '@angular/core/testing';
import { OAuthService, OAuthSuccessEvent } from 'angular-oauth2-oidc';
import { Subject } from 'rxjs';

import { APP_CONFIG, type AppConfig } from '../config/app-config.token';

import { AuthService } from './auth.service';

/** Build a compact JWT with the given payload (signature is a placeholder — unverified client-side). */
function makeJwt(payload: Record<string, unknown>): string {
  const b64url = (obj: unknown): string =>
    btoa(JSON.stringify(obj)).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
  return `${b64url({ alg: 'RS256' })}.${b64url(payload)}.sig`;
}

function appConfig(): AppConfig {
  return {
    production: false,
    gatewayOrigin: '',
    apiPublicPrefix: '/api/public',
    apiAdminPrefix: '/api/admin',
    auth: {
      issuer: 'http://localhost:8088/realms/wheretoeat',
      clientId: 'wheretoeat-spa',
      redirectUri: 'http://localhost:4200/',
      scope: 'openid profile email',
    },
    map: { provider: 'google-embed', googleMapsEmbedKey: '' },
    features: { analyticsEnabled: true, ratingSubmitEnabled: true, portalEnabled: true },
  };
}

/** A configurable OAuthService stub covering only the methods AuthService calls. */
function oauthStub(opts: {
  token?: string;
  valid?: boolean;
  /** When set, `loadDiscoveryDocumentAndTryLogin` rejects with this (Keycloak-unreachable case). */
  discoveryError?: unknown;
  /** Pushed onto the stub's `events$` subject so tests can drive the token lifecycle. */
  events?: Subject<OAuthSuccessEvent>;
}): Partial<OAuthService> {
  return {
    configure: vi.fn(),
    setupAutomaticSilentRefresh: vi.fn(),
    events: (opts.events ?? new Subject<OAuthSuccessEvent>()).asObservable(),
    loadDiscoveryDocumentAndTryLogin: vi.fn(async () => {
      if (opts.discoveryError !== undefined) {
        return Promise.reject(opts.discoveryError);
      }
      return opts.valid ?? false;
    }),
    loadDiscoveryDocumentAndLogin: vi.fn(async () => opts.valid ?? false),
    logOut: vi.fn() as unknown as OAuthService['logOut'],
    getAccessToken: vi.fn(() => opts.token ?? ''),
    hasValidAccessToken: vi.fn(() => opts.valid ?? false),
  };
}

function makeService(oauth: Partial<OAuthService>): AuthService {
  TestBed.configureTestingModule({
    providers: [
      AuthService,
      { provide: OAuthService, useValue: oauth },
      { provide: APP_CONFIG, useValue: appConfig() },
    ],
  });
  return TestBed.inject(AuthService);
}

describe('AuthService', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('starts unauthenticated with no roles', () => {
    const service = makeService(oauthStub({ valid: false }));
    expect(service.isAuthenticated()).toBe(false);
    expect(service.isAdmin()).toBe(false);
    expect([...service.roles()]).toEqual([]);
    expect(service.subject()).toBeNull();
  });

  it('derives auth state, subject and the admin role from a realm-role token', () => {
    const token = makeJwt({ sub: 'kc-sub-1', realm_access: { roles: ['admin'] } });
    const service = makeService(oauthStub({ token, valid: true }));

    service.syncFromToken();

    expect(service.isAuthenticated()).toBe(true);
    expect(service.subject()).toBe('kc-sub-1');
    expect([...service.roles()]).toEqual(['Admin']);
    expect(service.isAdmin()).toBe(true);
  });

  it('derives the admin role from a flattened role claim too', () => {
    const token = makeJwt({ sub: 'kc-sub-2', role: 'admin' });
    const service = makeService(oauthStub({ token, valid: true }));

    service.syncFromToken();

    expect(service.isAdmin()).toBe(true);
  });

  it('exposes the raw access token for the bearer interceptor', () => {
    const service = makeService(oauthStub({ token: 'tok-xyz', valid: true }));
    expect(service.accessToken()).toBe('tok-xyz');
  });

  it('folds /me-reported roles into the effective role set', () => {
    const token = makeJwt({ sub: 'kc-sub-3', realm_access: { roles: [] } });
    const service = makeService(oauthStub({ token, valid: true }));
    service.syncFromToken();
    expect(service.isAdmin()).toBe(false);

    service.refreshFromIdentity(['Admin']);

    expect(service.isAdmin()).toBe(true);
    expect([...service.roles()]).toContain('Admin');
  });

  it('clears session state on logout', () => {
    const token = makeJwt({ sub: 'kc-sub-4', realm_access: { roles: ['admin'] } });
    const service = makeService(oauthStub({ token, valid: true }));
    service.syncFromToken();
    expect(service.isAuthenticated()).toBe(true);

    service.logout();

    expect(service.isAuthenticated()).toBe(false);
    expect([...service.roles()]).toEqual([]);
    expect(service.subject()).toBeNull();
  });

  it('configures the OIDC client from APP_CONFIG on init (nothing hardcoded)', async () => {
    const oauth = oauthStub({ valid: false });
    const service = makeService(oauth);

    await service.init();

    expect(oauth.configure).toHaveBeenCalledOnce();
    const passed = (oauth.configure as ReturnType<typeof vi.fn>).mock.calls[0][0];
    expect(passed.issuer).toBe('http://localhost:8088/realms/wheretoeat');
    expect(passed.clientId).toBe('wheretoeat-spa');
    expect(passed.responseType).toBe('code');
  });

  it('starts unauthenticated (does not throw) when discovery fails / Keycloak is unreachable', async () => {
    const oauth = oauthStub({ discoveryError: new Error('network down') });
    const service = makeService(oauth);

    await expect(service.init()).resolves.toBeUndefined();

    expect(service.isAuthenticated()).toBe(false);
    expect(service.subject()).toBeNull();
    expect([...service.roles()]).toEqual([]);
  });

  it('re-syncs the auth signals when a silent refresh emits a token_received event', async () => {
    const events = new Subject<OAuthSuccessEvent>();
    // Discovery returns no session; a background refresh later hands the lib a valid admin token.
    const oauth = oauthStub({ valid: false, events });
    const service = makeService(oauth);

    await service.init();
    expect(service.isAuthenticated()).toBe(false);

    // Simulate the refresh: the lib now holds a valid admin token, then announces the new token.
    const token = makeJwt({ sub: 'kc-refreshed', realm_access: { roles: ['admin'] } });
    (oauth.hasValidAccessToken as ReturnType<typeof vi.fn>).mockReturnValue(true);
    (oauth.getAccessToken as ReturnType<typeof vi.fn>).mockReturnValue(token);
    events.next(new OAuthSuccessEvent('token_received'));

    expect(service.isAuthenticated()).toBe(true);
    expect(service.subject()).toBe('kc-refreshed');
    expect(service.isAdmin()).toBe(true);
  });
});
