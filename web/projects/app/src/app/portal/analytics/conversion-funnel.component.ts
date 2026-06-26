import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { TranslatePipe } from '@ngx-translate/core';

import type { ConversionFunnelDto } from 'core';

/** One rendered funnel stage: its label key, count, and width relative to the top stage. */
interface FunnelStage {
  readonly labelKey: string;
  readonly count: number;
  /** 0..100 width relative to the impression stage (the funnel mouth). */
  readonly widthPercent: number;
  readonly testId: string;
}

/**
 * Conversion-funnel metric panel (dumb / presentational) — Step 24 (§7.5).
 *
 * Renders the impression → card-open → action funnel as stage counts plus the two stage-to-stage
 * conversion rates. **Aggregates only** (invariant #11): the funnel is counts of events, never a
 * traceable per-user journey — the input DTO carries no actor/per-event field.
 *
 * Each stage is a labelled bar whose width is its share of the funnel mouth (impressions); the bars are
 * decorative, so every count and rate is also rendered as accessible text (the bars are
 * `aria-hidden`), giving a screen-reader user the same data without the chart.
 */
@Component({
  selector: 'app-analytics-funnel',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatIconModule, TranslatePipe],
  template: `
    <section class="dp-metric" data-testid="analytics-funnel" aria-labelledby="dp-funnel-heading">
      <h2 id="dp-funnel-heading" class="dp-metric__title">
        <mat-icon class="dp-metric__title-icon" aria-hidden="true">filter_alt</mat-icon>
        {{ 'portal.analytics.funnel.title' | translate }}
      </h2>

      <ol class="dp-funnel">
        @for (stage of stages(); track stage.testId) {
          <li class="dp-funnel__stage" [attr.data-testid]="stage.testId">
            <div class="dp-funnel__row">
              <span class="dp-funnel__label">{{ stage.labelKey | translate }}</span>
              <span class="dp-funnel__count">{{ stage.count }}</span>
            </div>
            <div
              class="dp-funnel__bar"
              aria-hidden="true"
              [style.width.%]="Math.max(stage.widthPercent, 4)"
            ></div>
          </li>
        }
      </ol>

      <dl class="dp-metric__stats">
        <div class="dp-metric__stat">
          <dt>{{ 'portal.analytics.funnel.openRate' | translate }}</dt>
          <dd data-testid="funnel-open-rate">{{ openRate() }}</dd>
        </div>
        <div class="dp-metric__stat">
          <dt>{{ 'portal.analytics.funnel.actionRate' | translate }}</dt>
          <dd data-testid="funnel-action-rate">{{ actionRate() }}</dd>
        </div>
      </dl>
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

    .dp-funnel {
      list-style: none;
      margin: 0;
      padding: 0;
      display: flex;
      flex-direction: column;
      gap: var(--dp-space-2);
    }

    .dp-funnel__row {
      display: flex;
      justify-content: space-between;
      gap: var(--dp-space-2);
    }

    .dp-funnel__label {
      color: var(--dp-color-on-surface-variant);
    }

    .dp-funnel__count {
      font-weight: var(--dp-font-weight-bold);
    }

    .dp-funnel__bar {
      margin-top: var(--dp-space-1);
      height: var(--dp-space-3);
      min-width: var(--dp-space-2);
      border-radius: var(--dp-radius-sm);
      background-color: var(--dp-color-primary);
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
  `,
})
export class ConversionFunnelComponent {
  /** `Math` exposed to the template for the per-stage minimum-width clamp. */
  protected readonly Math = Math;

  /** The aggregate funnel for the selected venue/window. */
  readonly funnel = input.required<ConversionFunnelDto>();

  /** The three funnel stages, each sized relative to the impression mouth. */
  readonly stages = computed<readonly FunnelStage[]>(() => {
    const f = this.funnel();
    const mouth = f.impressions > 0 ? f.impressions : 1;
    return [
      {
        labelKey: 'portal.analytics.funnel.impressions',
        count: f.impressions,
        widthPercent: 100,
        testId: 'funnel-impressions',
      },
      {
        labelKey: 'portal.analytics.funnel.cardOpens',
        count: f.cardOpens,
        widthPercent: (f.cardOpens / mouth) * 100,
        testId: 'funnel-card-opens',
      },
      {
        labelKey: 'portal.analytics.funnel.actions',
        count: f.actions,
        widthPercent: (f.actions / mouth) * 100,
        testId: 'funnel-actions',
      },
    ];
  });

  readonly openRate = computed<string>(
    () => `${(this.funnel().impressionToCardOpenRate * 100).toFixed(1)}%`,
  );
  readonly actionRate = computed<string>(
    () => `${(this.funnel().cardOpenToActionRate * 100).toFixed(1)}%`,
  );
}
