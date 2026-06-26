import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { TranslatePipe } from '@ngx-translate/core';

import type { RestaurantTrafficSummaryDto } from 'core';

/**
 * Visibility/traffic metric panel (dumb / presentational) — Step 24 (§7.5).
 *
 * Renders the impressions / card-opens / action-clicks counts and the impression→card-open CTR for
 * one venue over the selected window. **Aggregates only** (invariant #11): the input is a counts/ratios
 * DTO with no per-user/per-event field — there is nothing here that could imply individual users.
 *
 * The figures are shown as a labelled definition list (`<dl>`), which is the screen-reader-friendly
 * "data table" alternative to a chart for these scalar values; the CTR also renders as a labelled
 * progress bar so a sighted operator reads it at a glance while the percentage text remains the
 * accessible source of truth.
 */
@Component({
  selector: 'app-analytics-traffic',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatIconModule, TranslatePipe],
  template: `
    <section class="dp-metric" data-testid="analytics-traffic" aria-labelledby="dp-traffic-heading">
      <h2 id="dp-traffic-heading" class="dp-metric__title">
        <mat-icon class="dp-metric__title-icon" aria-hidden="true">visibility</mat-icon>
        {{ 'portal.analytics.traffic.title' | translate }}
      </h2>

      <dl class="dp-metric__stats">
        <div class="dp-metric__stat">
          <dt>{{ 'portal.analytics.traffic.impressions' | translate }}</dt>
          <dd data-testid="traffic-impressions">{{ summary().impressions }}</dd>
        </div>
        <div class="dp-metric__stat">
          <dt>{{ 'portal.analytics.traffic.cardOpens' | translate }}</dt>
          <dd data-testid="traffic-card-opens">{{ summary().cardOpens }}</dd>
        </div>
        <div class="dp-metric__stat">
          <dt>{{ 'portal.analytics.traffic.actions' | translate }}</dt>
          <dd data-testid="traffic-actions">{{ summary().actions }}</dd>
        </div>
        <div class="dp-metric__stat">
          <dt>{{ 'portal.analytics.traffic.ctr' | translate }}</dt>
          <dd data-testid="traffic-ctr">{{ ctrPercent() }}</dd>
        </div>
      </dl>

      <div
        class="dp-metric__bar"
        role="progressbar"
        [attr.aria-label]="'portal.analytics.traffic.ctr' | translate"
        [attr.aria-valuenow]="ctrRounded()"
        aria-valuemin="0"
        aria-valuemax="100"
      >
        <span class="dp-metric__bar-fill" [style.width.%]="ctrRounded()"></span>
      </div>
    </section>
  `,
  styles: `
    :host {
      display: block;
    }

    .dp-metric {
      display: flex;
      flex-direction: column;
      gap: var(--dp-space-3);
      padding: var(--dp-space-4);
      border: var(--dp-border-width-hairline) solid var(--dp-color-outline-variant);
      border-radius: var(--dp-radius-md);
      background-color: var(--dp-color-surface);
    }

    .dp-metric__title {
      display: flex;
      align-items: center;
      gap: var(--dp-space-2);
      margin: 0;
      font-size: var(--dp-font-size-subtitle);
      font-weight: var(--dp-font-weight-bold);
    }

    .dp-metric__title-icon {
      color: var(--dp-color-primary);
    }

    .dp-metric__stats {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(var(--dp-column-min-width-sm), 1fr));
      gap: var(--dp-space-3);
      margin: 0;
    }

    .dp-metric__stat dt {
      font-size: var(--dp-font-size-caption);
      color: var(--dp-color-on-surface-variant);
    }

    .dp-metric__stat dd {
      margin: var(--dp-space-1) 0 0;
      font-size: var(--dp-font-size-title);
      font-weight: var(--dp-font-weight-bold);
    }

    .dp-metric__bar {
      height: var(--dp-space-2);
      border-radius: var(--dp-radius-pill);
      background-color: var(--dp-color-surface-variant);
      overflow: hidden;
    }

    .dp-metric__bar-fill {
      display: block;
      height: 100%;
      background-color: var(--dp-color-primary);
    }
  `,
})
export class TrafficSummaryComponent {
  /** The aggregate traffic summary for the selected venue/window. */
  readonly summary = input.required<RestaurantTrafficSummaryDto>();

  /** CTR as a 0..100 integer for the progress bar's `aria-valuenow` / width. */
  readonly ctrRounded = computed<number>(() => Math.round(this.summary().ctr * 100));

  /** CTR rendered as a localised-ish percentage string (the accessible source of truth). */
  readonly ctrPercent = computed<string>(() => `${(this.summary().ctr * 100).toFixed(1)}%`);
}
