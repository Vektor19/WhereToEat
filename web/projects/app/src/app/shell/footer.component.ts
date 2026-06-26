import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

/**
 * Global footer — the **sole carrier** of the discreet data-accuracy notice
 * (CLAUDE.md invariant #12 / §10).
 *
 * The single `ⓘ` / `≈` notice lives here and ONLY here: prices are approximate
 * and the authoritative source is the venue. There are deliberately **no
 * per-item / per-price / per-photo disclaimers** anywhere else in the app, and
 * **no "updated X days ago"** copy at all. Later feature steps must not scatter
 * additional disclaimers — they point users to this one notice.
 *
 * Copy resolves from the runtime i18n catalog (Step 17) via the `footer.dataAccuracyNotice` key.
 */
@Component({
  selector: 'app-footer',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe],
  template: `
    <footer class="dp-footer" role="contentinfo">
      <p class="dp-footer__notice" data-testid="data-accuracy-notice">
        <span class="dp-footer__icon" aria-hidden="true">ⓘ</span>
        <span>{{ 'footer.dataAccuracyNotice' | translate }}</span>
      </p>
    </footer>
  `,
  styles: `
    .dp-footer {
      padding: var(--dp-space-4) var(--dp-layout-gutter);
      background-color: var(--dp-color-surface-variant);
      border-top: var(--dp-border-width-hairline) solid var(--dp-color-outline-variant);
      color: var(--dp-color-on-surface-variant);
    }

    .dp-footer__notice {
      display: flex;
      gap: var(--dp-space-2);
      align-items: baseline;
      max-width: var(--dp-layout-content-max);
      margin: 0 auto;
      font-size: var(--dp-font-size-caption);
      line-height: var(--dp-line-height-base);
    }

    .dp-footer__icon {
      font-size: var(--dp-font-size-body);
      flex: 0 0 auto;
    }
  `,
})
export class FooterComponent {}
