import { signal, type WritableSignal } from '@angular/core';
import { TestBed, type ComponentFixture } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { AuthService, type UserRole } from 'core';

import { PortalShellComponent } from './portal-shell.component';
import { PORTAL_NAV } from './portal-nav';

/**
 * A controllable {@link AuthService} double exposing only the members the shell reads/calls:
 * the session signals (`isAuthenticated`/`isAdmin`/`subject`/`roles`) and `login`/`logout`. Tests
 * drive the signals to render the admin-nav, the anonymous chrome, and the non-admin denied state.
 */
class AuthServiceStub {
  readonly isAuthenticated: WritableSignal<boolean> = signal(true);
  readonly isAdmin: WritableSignal<boolean> = signal(true);
  readonly subject: WritableSignal<string | null> = signal('op-123');
  readonly roles: WritableSignal<readonly UserRole[]> = signal<readonly UserRole[]>(['Admin']);
  readonly loginCalls: number = 0;
  login = vi.fn(async () => undefined);
  logout = vi.fn(() => undefined);
}

describe('PortalShellComponent', () => {
  let auth: AuthServiceStub;

  beforeEach(async () => {
    auth = new AuthServiceStub();
    await TestBed.configureTestingModule({
      imports: [PortalShellComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        { provide: AuthService, useValue: auth },
      ],
    }).compileComponents();
  });

  function render(): ComponentFixture<PortalShellComponent> {
    const fixture = TestBed.createComponent(PortalShellComponent);
    fixture.detectChanges();
    return fixture;
  }

  function q(fixture: ComponentFixture<PortalShellComponent>, testid: string): HTMLElement | null {
    return fixture.nativeElement.querySelector(`[data-testid="${testid}"]`);
  }

  it('renders the management nav skeleton (one entry per portal area)', () => {
    const fixture = render();
    for (const item of PORTAL_NAV) {
      expect(q(fixture, `portal-nav-${item.testId}`)).not.toBeNull();
    }
  });

  it('shows the signed-in subject and role for an admin session', () => {
    const fixture = render();
    expect(q(fixture, 'portal-identity')).not.toBeNull();
    expect((q(fixture, 'portal-subject')?.textContent ?? '').trim()).toBe('op-123');
    expect((q(fixture, 'portal-role')?.textContent ?? '').trim()).toBe('Admin');
  });

  it('wires the sign-out affordance to AuthService.logout', () => {
    const fixture = render();
    const logout = q(fixture, 'portal-logout');
    expect(logout).not.toBeNull();
    logout?.click();
    expect(auth.logout).toHaveBeenCalledOnce();
  });

  it('shows a sign-in affordance wired to AuthService.login when not authenticated', () => {
    auth.isAuthenticated.set(false);
    auth.isAdmin.set(false);
    auth.subject.set(null);
    auth.roles.set([]);
    const fixture = render();

    expect(q(fixture, 'portal-identity')).toBeNull();
    const login = q(fixture, 'portal-login');
    expect(login).not.toBeNull();
    login?.click();
    expect(auth.login).toHaveBeenCalledOnce();
  });

  it('renders an accessible "not authorized" state for an authenticated non-admin (no nav)', () => {
    auth.isAuthenticated.set(true);
    auth.isAdmin.set(false);
    auth.subject.set('user-9');
    auth.roles.set(['User']);
    const fixture = render();

    const denied = q(fixture, 'portal-denied');
    expect(denied).not.toBeNull();
    expect(denied?.getAttribute('role')).toBe('alert');
    // The nav skeleton is not rendered for a non-admin.
    expect(q(fixture, 'portal-nav-dashboard')).toBeNull();
    // The identity (who they are signed in as) is still surfaced.
    expect(q(fixture, 'portal-identity')).not.toBeNull();
  });

  it('exposes a keyboard skip link to the portal content', () => {
    const fixture = render();
    const skip = fixture.nativeElement.querySelector('a.dp-skip-link') as HTMLAnchorElement | null;
    expect(skip).not.toBeNull();
    expect(skip?.getAttribute('href')).toBe('#dp-portal-content');
  });

  it('contains no "updated X days ago" copy', () => {
    const fixture = render();
    const text = (fixture.nativeElement.textContent ?? '').toLowerCase();
    expect(text).not.toContain('оновлено');
    expect(text).not.toContain('updated');
  });
});
