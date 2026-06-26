import { runInInjectionContext, Injector } from '@angular/core';
import { TestBed } from '@angular/core/testing';

import { AuthService } from './auth.service';
import { adminGuard } from './admin.guard';

/** A controllable AuthService stub exposing the three members the guard reads. */
function authStub(opts: {
  isAdmin: boolean;
  isAuthenticated: boolean;
  login?: () => Promise<void>;
}): Pick<AuthService, 'isAdmin' | 'isAuthenticated' | 'login'> {
  return {
    isAdmin: (() => opts.isAdmin) as AuthService['isAdmin'],
    isAuthenticated: (() => opts.isAuthenticated) as AuthService['isAuthenticated'],
    login: opts.login ?? (async () => undefined),
  };
}

function runGuard(auth: Partial<AuthService>): boolean {
  TestBed.configureTestingModule({
    providers: [{ provide: AuthService, useValue: auth }],
  });
  const injector = TestBed.inject(Injector);
  // The functional guard takes (route, state); the guard ignores both, so undefined is safe.
  return runInInjectionContext(injector, () =>
    adminGuard(undefined as never, undefined as never),
  ) as boolean;
}

describe('adminGuard', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('admits a session that carries the Admin role', () => {
    expect(runGuard(authStub({ isAdmin: true, isAuthenticated: true }))).toBe(true);
  });

  it('denies an authenticated user-only (non-admin) token without re-prompting login', () => {
    const login = vi.fn(async () => undefined);
    expect(runGuard(authStub({ isAdmin: false, isAuthenticated: true, login }))).toBe(false);
    expect(login).not.toHaveBeenCalled();
  });

  it('denies an anonymous caller and kicks off the login flow', () => {
    const login = vi.fn(async () => undefined);
    expect(runGuard(authStub({ isAdmin: false, isAuthenticated: false, login }))).toBe(false);
    expect(login).toHaveBeenCalledOnce();
  });
});
