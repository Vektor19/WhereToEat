import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatToolbarModule } from '@angular/material/toolbar';
import { TranslatePipe } from '@ngx-translate/core';

import { AuthService } from 'core';

import { PORTAL_NAV, type PortalNavItem } from './portal-nav';

/**
 * Operator portal shell (Surface B, web-only) — Step 18.
 *
 * The management-portal chrome that wraps every admin/operator feature: a top bar (brand +
 * signed-in identity + sign-in/out affordance) and a side-nav of the management areas, with the
 * routed feature rendered into the content `<router-outlet>`. It is deliberately **separate** from
 * the consumer {@link AppShellComponent} — the portal is web-only and does not travel to the future
 * RN app, so it may be Angular-heavy and its own surface.
 *
 * Role-gating context: this shell is only ever reached through the {@link adminGuard} on the portal
 * route (an admin/operator session). It still surfaces the session state — the signed-in `sub` and
 * roles from {@link AuthService} — and a sign-out (and, defensively, a sign-in) affordance, so the
 * operator can see who they are signed in as and end the session. A non-admin who somehow lands here
 * sees the explicit "not authorized" state instead of the nav.
 *
 * Boundary: portal code MUST NOT import consumer internals; the shared session/guard live in `core`
 * and are consumed through its public-api barrel. The nav is data-driven ({@link PORTAL_NAV}) so the
 * Step 19/20/24 features (and a future owner-persona subset — design Risk #3) extend the list, not
 * this component. All copy resolves from the runtime i18n catalogs (Step 17); the chrome is themed
 * entirely from design tokens (`var(--dp-*)`) — no raw hex/spacing literals — and is mobile-first
 * with semantic landmarks for keyboard / screen-reader navigation (WCAG).
 */
@Component({
  selector: 'app-portal-shell',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    MatToolbarModule,
    MatSidenavModule,
    MatListModule,
    MatButtonModule,
    MatIconModule,
    TranslatePipe,
  ],
  template: `
    <a class="dp-skip-link" href="#dp-portal-content">{{ 'portal.skipToContent' | translate }}</a>

    <div class="dp-portal">
      <mat-toolbar class="dp-portal__topbar" color="primary">
        <span class="dp-portal__brand">{{ 'portal.brand' | translate }}</span>
        <span class="dp-portal__spacer"></span>

        @if (isAuthenticated()) {
          <span class="dp-portal__identity" data-testid="portal-identity">
            <mat-icon aria-hidden="true">account_circle</mat-icon>
            <span class="dp-portal__sub" data-testid="portal-subject">{{ subjectLabel() }}</span>
            <span class="dp-portal__role" data-testid="portal-role">{{ roleLabel() }}</span>
          </span>
          <button
            mat-stroked-button
            type="button"
            class="dp-portal__auth"
            data-testid="portal-logout"
            (click)="logout()"
          >
            <mat-icon aria-hidden="true">logout</mat-icon>
            {{ 'portal.signOut' | translate }}
          </button>
        } @else {
          <button
            mat-stroked-button
            type="button"
            class="dp-portal__auth"
            data-testid="portal-login"
            (click)="login()"
          >
            <mat-icon aria-hidden="true">login</mat-icon>
            {{ 'portal.signIn' | translate }}
          </button>
        }
      </mat-toolbar>

      @if (isAdmin()) {
        <mat-sidenav-container class="dp-portal__body">
          <mat-sidenav
            class="dp-portal__nav"
            mode="side"
            opened
            [attr.aria-label]="'portal.navLabel' | translate"
          >
            <mat-nav-list>
              @for (item of nav; track item.path) {
                <a
                  mat-list-item
                  [routerLink]="item.path"
                  routerLinkActive="dp-portal__nav-item--active"
                  [routerLinkActiveOptions]="{ exact: item.exact }"
                  [attr.data-testid]="'portal-nav-' + item.testId"
                >
                  <mat-icon matListItemIcon aria-hidden="true">{{ item.icon }}</mat-icon>
                  <span matListItemTitle>{{ item.labelKey | translate }}</span>
                </a>
              }
            </mat-nav-list>
          </mat-sidenav>

          <mat-sidenav-content>
            <main id="dp-portal-content" class="dp-portal__content" tabindex="-1">
              <router-outlet />
            </main>
          </mat-sidenav-content>
        </mat-sidenav-container>
      } @else {
        <main id="dp-portal-content" class="dp-portal__content" tabindex="-1">
          <section class="dp-portal__denied" data-testid="portal-denied" role="alert">
            <mat-icon class="dp-portal__denied-icon" aria-hidden="true">lock</mat-icon>
            <h1 class="dp-portal__denied-title">{{ 'portal.denied.title' | translate }}</h1>
            <p class="dp-portal__denied-body">{{ 'portal.denied.body' | translate }}</p>
          </section>
        </main>
      }
    </div>
  `,
  styles: `
    // The skip-link styling is a global landmark pattern (see styles.scss), shared
    // with the consumer shell — it is intentionally NOT duplicated here.
    @use 'tokens';

    :host {
      display: block;
    }

    .dp-portal {
      display: flex;
      flex-direction: column;
      min-height: 100vh;
    }

    .dp-portal__topbar {
      position: sticky;
      top: 0;
      z-index: var(--dp-z-topbar);
      min-height: var(--dp-layout-topbar-height);
      background-color: var(--dp-color-primary);
      color: var(--dp-color-on-primary);
      box-shadow: var(--dp-elevation-1);
      gap: var(--dp-space-3);
    }

    .dp-portal__brand {
      font-weight: var(--dp-font-weight-bold);
      font-size: var(--dp-font-size-subtitle);
      letter-spacing: 0.01em;
    }

    .dp-portal__spacer {
      flex: 1 1 auto;
    }

    .dp-portal__identity {
      display: inline-flex;
      align-items: center;
      gap: var(--dp-space-1);
      font-size: var(--dp-font-size-caption);
    }

    .dp-portal__sub {
      font-weight: var(--dp-font-weight-medium);
      max-width: 16ch;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }

    .dp-portal__role {
      opacity: 0.85;
    }

    .dp-portal__body {
      flex: 1 1 auto;
    }

    .dp-portal__nav {
      width: var(--dp-layout-portal-nav-width);
      border-right: var(--dp-border-width-hairline) solid var(--dp-color-outline-variant);
    }

    .dp-portal__nav-item--active {
      background-color: var(--dp-color-surface-variant);
      font-weight: var(--dp-font-weight-medium);
    }

    .dp-portal__content {
      display: block;
      padding: var(--dp-space-4) var(--dp-layout-gutter);
      outline: none;
    }

    .dp-portal__denied {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: var(--dp-space-3);
      max-width: var(--dp-layout-content-max);
      margin: var(--dp-space-8) auto;
      padding: var(--dp-space-6);
      text-align: center;
      color: var(--dp-color-on-surface-variant);
    }

    .dp-portal__denied-icon {
      width: var(--dp-space-8);
      height: var(--dp-space-8);
      font-size: var(--dp-space-8);
    }

    .dp-portal__denied-title {
      margin: 0;
      font-size: var(--dp-font-size-title);
      font-weight: var(--dp-font-weight-bold);
      color: var(--dp-color-on-surface);
    }

    .dp-portal__denied-body {
      margin: 0;
      font-size: var(--dp-font-size-body);
    }

    @media (min-width: tokens.$dp-breakpoint-md) {
      .dp-portal__content {
        padding: var(--dp-space-6) var(--dp-space-8);
      }
    }
  `,
})
export class PortalShellComponent {
  private readonly auth = inject(AuthService);

  /** The data-driven nav of management areas (Steps 19/20/24 extend this list). */
  readonly nav: readonly PortalNavItem[] = PORTAL_NAV;

  // ── Session state surfaced in the chrome (the guard already gated the route). ──
  readonly isAuthenticated = this.auth.isAuthenticated;
  readonly isAdmin = this.auth.isAdmin;

  /** The signed-in subject (`sub`), or a neutral placeholder when none is held. */
  readonly subjectLabel = (): string => this.auth.subject() ?? '—';

  /** The signed-in roles as a compact, comma-separated label (empty string when none). */
  readonly roleLabel = (): string => this.auth.roles().join(', ');

  /** Begin the Authorization Code + PKCE flow (defensive — the guard normally handles sign-in). */
  login(): void {
    void this.auth.login();
  }

  /** Clear the local session and (when configured) redirect to the IdP end-session endpoint. */
  logout(): void {
    this.auth.logout();
  }
}
