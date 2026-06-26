import { Component, signal, type WritableSignal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter, type Route, type Routes } from '@angular/router';
import { AuthService, adminGuard } from 'core';

import { portalRoutes } from './portal.routes';

/**
 * A throwaway feature page standing in for the menu/protection/etc. children Steps 19/20/24 will hang
 * off the portal parent. It exists only so a navigation can reach a *child* path and thereby trigger
 * the parent's `canActivateChild`; the guard fires before any component loads, so its body is empty.
 */
@Component({ selector: 'app-test-child', template: '' })
class TestChildComponent {}

/**
 * A controllable {@link AuthService} double for the guard the portal subtree carries. Only the three
 * members {@link adminGuard} reads are provided; the role/auth signals are driven per scenario.
 */
class AuthServiceStub {
  readonly isAdmin: WritableSignal<boolean> = signal(false);
  readonly isAuthenticated: WritableSignal<boolean> = signal(false);
  login = vi.fn(async () => undefined);
}

/**
 * The real portal parent route with a throwaway `menu` child appended, so a navigation to a *child*
 * deep-link (`/menu`) actually traverses the parent's `canActivateChild`. The real `canActivate` /
 * `canActivateChild` / lazy `loadComponent` wiring is preserved untouched — only an extra eager child
 * is added for the test (the guard runs before any child component would load).
 */
function routesWithTestChild(): Routes {
  const parent = portalRoutes[0];
  return [
    {
      ...parent,
      children: [...(parent.children ?? []), { path: 'menu', component: TestChildComponent }],
    },
  ];
}

describe('portalRoutes (lazy, admin-gated boundary)', () => {
  let auth: AuthServiceStub;
  let router: Router;

  function configure(routes: Routes): void {
    TestBed.configureTestingModule({
      providers: [provideRouter(routes), { provide: AuthService, useValue: auth }],
    });
    router = TestBed.inject(Router);
  }

  beforeEach(() => {
    auth = new AuthServiceStub();
    configure(portalRoutes);
  });

  /** The single parent route the whole portal subtree hangs off. */
  function parentRoute(): Route {
    expect(portalRoutes.length).toBe(1);
    return portalRoutes[0];
  }

  it('lazy-loads the portal shell as a separate chunk (loadComponent, not eager)', () => {
    const route = parentRoute();
    // The shell is referenced via a dynamic import, never an eager `component` — so the portal is its
    // own lazy chunk and the consumer bundle does not pull it in.
    expect(typeof route.loadComponent).toBe('function');
    expect(route.component).toBeUndefined();
    // The landing/dashboard child is likewise lazily loaded.
    expect(route.children?.[0].loadComponent).toBeTypeOf('function');
  });

  it('registers the Step 19/20/24 management features as lazy children under the gated parent', () => {
    const children = parentRoute().children ?? [];
    // Each management feature is its own lazy chunk (`loadChildren`) hanging off the gated parent, so
    // it inherits the `adminGuard` without re-declaring it and the consumer bundle never pulls it in.
    // `verified` / `ads` are the Step 20 monetization features; `analytics` is the Step 24 dashboard.
    for (const path of [
      'menu',
      'protection',
      'address',
      'photos',
      'verified',
      'ads',
      'analytics',
    ]) {
      const child = children.find((c) => c.path === path);
      expect(child, `missing route for "${path}"`).toBeDefined();
      expect(child?.loadChildren).toBeTypeOf('function');
    }
  });

  it('guards the whole subtree with the adminGuard on the parent route', () => {
    const route = parentRoute();
    // The exact `core` guard function is wired (not a re-implementation) on the parent activation.
    expect(route.canActivate).toContain(adminGuard);
    // Children inherit the gate too (defence in depth for nested feature routes): it must be the
    // *same* `core` guard, not merely some guard of the right arity — a wrong/permissive guard of
    // length 1 would otherwise slip through.
    expect(route.canActivateChild?.length).toBe(1);
    expect(route.canActivateChild).toContain(adminGuard);
  });

  it('admits an admin session to the portal', async () => {
    auth.isAdmin.set(true);
    auth.isAuthenticated.set(true);
    const ok = await router.navigateByUrl('/');
    expect(ok).toBe(true);
    expect(router.url).toBe('/');
  });

  it('denies an authenticated non-admin session (navigation blocked, no login prompt)', async () => {
    auth.isAdmin.set(false);
    auth.isAuthenticated.set(true);
    const ok = await router.navigateByUrl('/');
    expect(ok).toBe(false);
    expect(auth.login).not.toHaveBeenCalled();
  });

  it('denies an anonymous caller and initiates the login flow', async () => {
    auth.isAdmin.set(false);
    auth.isAuthenticated.set(false);
    const ok = await router.navigateByUrl('/');
    expect(ok).toBe(false);
    expect(auth.login).toHaveBeenCalledOnce();
  });

  // Deep-link gating: the parent's `canActivateChild` is what Steps 19/20/24 children rely on, but a
  // navigation to the parent path ('/') only runs `canActivate`. These tests navigate to a *child*
  // path ('/menu') so `canActivateChild` actually fires, proving nested feature routes inherit the
  // gate.
  describe('child deep-link inherits the gate via canActivateChild', () => {
    beforeEach(() => {
      // The outer `beforeEach` already configured (and instantiated) TestBed with the parent-only
      // routes; reset before re-providing the variant that carries a child path.
      TestBed.resetTestingModule();
      configure(routesWithTestChild());
    });

    it('admits an admin session to a child feature route', async () => {
      auth.isAdmin.set(true);
      auth.isAuthenticated.set(true);
      const ok = await router.navigateByUrl('/menu');
      expect(ok).toBe(true);
      expect(router.url).toBe('/menu');
    });

    it('blocks an authenticated non-admin from a child feature route (no login prompt)', async () => {
      auth.isAdmin.set(false);
      auth.isAuthenticated.set(true);
      const ok = await router.navigateByUrl('/menu');
      expect(ok).toBe(false);
      expect(router.url).not.toBe('/menu');
      expect(auth.login).not.toHaveBeenCalled();
    });

    it('blocks an anonymous deep-link to a child feature route and initiates login', async () => {
      auth.isAdmin.set(false);
      auth.isAuthenticated.set(false);
      const ok = await router.navigateByUrl('/menu');
      expect(ok).toBe(false);
      expect(router.url).not.toBe('/menu');
      expect(auth.login).toHaveBeenCalledOnce();
    });
  });
});
