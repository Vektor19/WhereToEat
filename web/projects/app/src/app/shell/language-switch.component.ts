import { UpperCasePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { TranslatePipe } from '@ngx-translate/core';
import { LocaleService } from 'core';

/**
 * Language switch (Step 17) — the real switcher that replaces the disabled placeholder slot from the
 * app-shell (Step 2).
 *
 * It lists the {@link LocaleService.locales supported locales} (autonym labels) in an accessible
 * Material menu and, on selection, calls {@link LocaleService.use} — which swaps the active catalog at
 * runtime (no rebuild — the reason the plan chose runtime catalogs over compile-time `$localize`),
 * sets `document.documentElement.lang`, drives the locale-aware formatters, and **persists** the
 * choice. Even though only `uk` is fully populated at launch, the mechanism works end-to-end, so
 * adding a locale is config-only.
 *
 * Accessibility: a labelled menu trigger (`aria-label` + `aria-haspopup`), each item shows the active
 * locale with a check, and the trigger exposes the current locale code as its visible affordance.
 */
@Component({
  selector: 'app-language-switch',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [UpperCasePipe, MatButtonModule, MatIconModule, MatMenuModule, TranslatePipe],
  template: `
    <button
      mat-button
      type="button"
      class="dp-lang__trigger"
      data-testid="lang-switch"
      [matMenuTriggerFor]="langMenu"
      [attr.aria-label]="'app.chooseLanguage' | translate"
      aria-haspopup="menu"
    >
      <mat-icon aria-hidden="true">language</mat-icon>
      <span class="dp-lang__code">{{ activeCode() | uppercase }}</span>
    </button>

    <mat-menu #langMenu="matMenu">
      @for (locale of locales; track locale.code) {
        <button
          mat-menu-item
          type="button"
          [attr.data-testid]="'lang-option-' + locale.code"
          [attr.aria-current]="locale.code === activeCode() ? 'true' : null"
          (click)="select(locale.code)"
        >
          @if (locale.code === activeCode()) {
            <mat-icon aria-hidden="true">check</mat-icon>
          }
          <span>{{ locale.label }}</span>
        </button>
      }
    </mat-menu>
  `,
  styles: `
    .dp-lang__trigger {
      color: var(--dp-color-on-primary);
    }

    .dp-lang__code {
      margin-inline-start: var(--dp-space-1);
      font-weight: var(--dp-font-weight-medium);
    }
  `,
})
export class LanguageSwitchComponent {
  private readonly locale = inject(LocaleService);

  /** The locales to offer (autonym labels), in display order. */
  readonly locales = this.locale.locales;

  /** The active locale code (drives the trigger label and the active-item check). */
  readonly activeCode = this.locale.activeLocale;

  /** Switch the app to `code` (catalog + `lang` + formatters + persistence). */
  select(code: string): void {
    this.locale.use(code);
  }
}
