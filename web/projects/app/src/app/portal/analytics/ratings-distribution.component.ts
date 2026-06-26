import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { TranslatePipe } from '@ngx-translate/core';

import type { RatingsDistributionDto } from 'core';

/**
 * Ratings-distribution metric panel (dumb / presentational) — Step 24 (§7.5).
 *
 * Renders the cumulative all-time rating count and average score from the materialized aggregate
 * (invariant #6). **Aggregate-only** (invariant #11): the input carries a count, a score sum, and the
 * derived average — never an individual user's score or identity. The average is shown to one decimal
 * with a star icon for glance value; the count is the accessible sample-size context.
 */
@Component({
  selector: 'app-analytics-ratings',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatIconModule, TranslatePipe],
  template: `
    <section class="dp-metric" data-testid="analytics-ratings" aria-labelledby="dp-ratings-heading">
      <h2 id="dp-ratings-heading" class="dp-metric__title">
        <mat-icon class="dp-metric__title-icon" aria-hidden="true">star_rate</mat-icon>
        {{ 'portal.analytics.ratings.title' | translate }}
      </h2>

      @if (distribution().ratingCount > 0) {
        <dl class="dp-metric__stats">
          <div class="dp-metric__stat">
            <dt>{{ 'portal.analytics.ratings.average' | translate }}</dt>
            <dd data-testid="ratings-average">
              <mat-icon class="dp-ratings__star" aria-hidden="true">star</mat-icon>
              {{ average() }}
            </dd>
          </div>
          <div class="dp-metric__stat">
            <dt>{{ 'portal.analytics.ratings.count' | translate }}</dt>
            <dd data-testid="ratings-count">{{ distribution().ratingCount }}</dd>
          </div>
        </dl>
      } @else {
        <p class="dp-metric__empty" data-testid="ratings-empty">
          {{ 'portal.analytics.ratings.empty' | translate }}
        </p>
      }
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
      display: flex;
      align-items: center;
      gap: var(--dp-space-1);
      margin: var(--dp-space-1) 0 0;
      font-size: var(--dp-font-size-title);
      font-weight: var(--dp-font-weight-bold);
    }

    .dp-ratings__star {
      color: var(--dp-color-primary);
    }

    .dp-metric__empty {
      margin: 0;
      color: var(--dp-color-on-surface-variant);
    }
  `,
})
export class RatingsDistributionComponent {
  /** The aggregate ratings distribution for the selected venue. */
  readonly distribution = input.required<RatingsDistributionDto>();

  /** The average score to one decimal (the backend already smooths/derives it — invariant #6). */
  readonly average = computed<string>(() => this.distribution().averageScore.toFixed(1));
}
