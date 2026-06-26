import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatButtonModule } from '@angular/material/button';
import { TranslatePipe } from '@ngx-translate/core';
import { FooterComponent } from './footer.component';
import { LanguageSwitchComponent } from './language-switch.component';

/**
 * Responsive, mobile-first application shell.
 *
 * Structure: a sticky top bar (brand + a placeholder **language-switch slot** for
 * Step 17), the routed content outlet, and the global {@link FooterComponent}
 * that hosts the single discreet data-accuracy notice (invariant #12 / §10).
 *
 * The shell is themed entirely from design tokens (`var(--dp-*)`) — no raw
 * hex/spacing literals — and is fluid across phone/tablet/desktop breakpoints
 * via the token-driven layout primitives. Semantic landmarks (`header`/`main`/
 * `footer`) and a skip link support keyboard and screen-reader navigation (WCAG).
 * All copy resolves from the runtime i18n catalogs (Step 17); the language-switch
 * slot hosts the real {@link LanguageSwitchComponent}.
 */
@Component({
  selector: 'app-shell',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterOutlet,
    MatToolbarModule,
    MatButtonModule,
    TranslatePipe,
    FooterComponent,
    LanguageSwitchComponent,
  ],
  template: `
    <a class="dp-skip-link" href="#dp-main-content">{{ 'app.skipToContent' | translate }}</a>

    <div class="dp-shell">
      <mat-toolbar class="dp-shell__topbar" color="primary">
        <span class="dp-shell__brand">{{ 'app.brand' | translate }}</span>
        <span class="dp-shell__spacer"></span>
        <!-- Language-switch slot — the real runtime switcher (Step 17). -->
        <div
          class="dp-shell__lang-slot"
          data-testid="lang-switch-slot"
          [attr.aria-label]="'app.languageLabel' | translate"
        >
          <app-language-switch />
        </div>
      </mat-toolbar>

      <main id="dp-main-content" class="dp-shell__content" tabindex="-1">
        <div class="dp-shell__container">
          <router-outlet />
        </div>
      </main>

      <app-footer />
    </div>
  `,
  styles: `
    // Breakpoints are Sass vars (CSS custom props can't be read in @media).
    // The skip-link styling is a global landmark pattern (see styles.scss), shared
    // with the operator shell — it is intentionally NOT duplicated here.
    @use 'tokens';

    .dp-shell {
      display: flex;
      flex-direction: column;
      min-height: 100vh;
    }

    .dp-shell__topbar {
      position: sticky;
      top: 0;
      z-index: var(--dp-z-topbar);
      min-height: var(--dp-layout-topbar-height);
      background-color: var(--dp-color-primary);
      color: var(--dp-color-on-primary);
      box-shadow: var(--dp-elevation-1);
    }

    .dp-shell__brand {
      font-weight: var(--dp-font-weight-bold);
      font-size: var(--dp-font-size-subtitle);
      letter-spacing: 0.01em;
    }

    .dp-shell__spacer {
      flex: 1 1 auto;
    }

    .dp-shell__content {
      flex: 1 1 auto;
      outline: none;
    }

    .dp-shell__container {
      width: 100%;
      max-width: var(--dp-layout-content-max);
      margin: 0 auto;
      /* Mobile-first gutters; widen at larger breakpoints. */
      padding: var(--dp-space-4) var(--dp-layout-gutter);
    }

    @media (min-width: tokens.$dp-breakpoint-sm) {
      .dp-shell__container {
        padding: var(--dp-space-5) var(--dp-space-6);
      }
    }

    @media (min-width: tokens.$dp-breakpoint-md) {
      .dp-shell__container {
        padding: var(--dp-space-6) var(--dp-space-8);
      }
    }
  `,
})
export class AppShellComponent {}
