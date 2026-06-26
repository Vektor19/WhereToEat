import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { TranslatePipe } from '@ngx-translate/core';

import { CurrencyFormatter, LocaleService, type PricePositioningDto } from 'core';

/** A price row prepared for display: pre-formatted money strings + the cheaper/dearer marker. */
interface PriceRow {
  readonly dishId: string;
  readonly venuePrice: string;
  readonly medianPrice: string;
  readonly delta: string;
  /** `'cheaper' | 'dearer' | 'equal' | 'unknown'` — drives the marker + accessible label key. */
  readonly position: 'cheaper' | 'dearer' | 'equal' | 'unknown';
}

/**
 * Price-positioning metric panel (dumb / presentational) — Step 24 (§7.5).
 *
 * Renders each carried dish's price against the area/category median the engine materializes, with a
 * signed delta and a cheaper/dearer marker, so the operator sees where the venue sits versus the
 * market. **No behavioural/per-user data** (invariant #11): the input is purely the venue's own prices
 * and the precomputed market medians. Money is formatted by the venue's own `currency` via the shared
 * {@link CurrencyFormatter} — never a hardcoded symbol (invariant §3 / §10).
 *
 * A table (not a chart) keeps the per-dish numbers screen-reader-native; the cheaper/dearer state is
 * conveyed by text, not colour alone.
 */
@Component({
  selector: 'app-analytics-price-positioning',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatIconModule, TranslatePipe],
  template: `
    <section class="dp-metric" data-testid="analytics-price" aria-labelledby="dp-price-heading">
      <h2 id="dp-price-heading" class="dp-metric__title">
        <mat-icon class="dp-metric__title-icon" aria-hidden="true">sell</mat-icon>
        {{ 'portal.analytics.price.title' | translate }}
      </h2>
      <p class="dp-metric__note">{{ 'portal.analytics.price.note' | translate }}</p>

      @if (rows().length > 0) {
        <table>
          <caption class="dp-visually-hidden">
            {{
              'portal.analytics.price.title' | translate
            }}
          </caption>
          <thead>
            <tr>
              <th scope="col">{{ 'portal.analytics.price.dish' | translate }}</th>
              <th scope="col" class="dp-num">
                {{ 'portal.analytics.price.venuePrice' | translate }}
              </th>
              <th scope="col" class="dp-num">{{ 'portal.analytics.price.median' | translate }}</th>
              <th scope="col" class="dp-num">{{ 'portal.analytics.price.delta' | translate }}</th>
            </tr>
          </thead>
          <tbody>
            @for (row of rows(); track row.dishId) {
              <tr [attr.data-testid]="'price-row-' + row.dishId">
                <td>{{ row.dishId }}</td>
                <td class="dp-num">{{ row.venuePrice }}</td>
                <td class="dp-num">{{ row.medianPrice }}</td>
                <td class="dp-num" [class]="'dp-pos--' + row.position">
                  <span>{{ row.delta }}</span>
                  <span class="dp-pos__label">{{
                    positionLabelKey(row.position) | translate
                  }}</span>
                </td>
              </tr>
            }
          </tbody>
        </table>
      } @else {
        <p class="dp-metric__empty" data-testid="price-empty">
          {{ 'portal.analytics.price.empty' | translate }}
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

    .dp-metric__note {
      margin: 0;
      font-size: var(--dp-font-size-caption);
      color: var(--dp-color-on-surface-variant);
    }

    table {
      width: 100%;
      border-collapse: collapse;
    }

    th,
    td {
      padding: var(--dp-space-2);
      text-align: start;
      border-bottom: var(--dp-border-width-hairline) solid var(--dp-color-outline-variant);
      font-size: var(--dp-font-size-body);
    }

    th {
      color: var(--dp-color-on-surface-variant);
      font-weight: var(--dp-font-weight-medium);
    }

    .dp-num {
      text-align: end;
      white-space: nowrap;
    }

    .dp-pos__label {
      display: block;
      font-size: var(--dp-font-size-caption);
    }

    .dp-pos--cheaper {
      color: var(--dp-color-success, var(--dp-color-primary));
    }

    .dp-pos--dearer {
      color: var(--dp-color-error);
    }

    .dp-metric__empty {
      margin: 0;
      color: var(--dp-color-on-surface-variant);
    }

    .dp-visually-hidden {
      position: absolute;
      width: 1px;
      height: 1px;
      margin: -1px;
      padding: 0;
      overflow: hidden;
      clip: rect(0, 0, 0, 0);
      white-space: nowrap;
      border: 0;
    }
  `,
})
export class PricePositioningComponent {
  private readonly currency = inject(CurrencyFormatter);
  private readonly locale = inject(LocaleService);

  /** The aggregate price positioning for the selected venue. */
  readonly positioning = input.required<PricePositioningDto>();

  /** Per-dish rows with money pre-formatted in the venue's currency and a cheaper/dearer marker. */
  readonly rows = computed<readonly PriceRow[]>(() => {
    const locale = this.locale.activeLocale();
    return this.positioning().dishes.map((dish) => {
      const hasMedian = dish.medianPrice !== null && dish.deltaFromMedian !== null;
      const delta = dish.deltaFromMedian;
      const position = !hasMedian
        ? 'unknown'
        : delta! < 0
          ? 'cheaper'
          : delta! > 0
            ? 'dearer'
            : 'equal';
      return {
        dishId: dish.dishId,
        venuePrice: this.currency.format(dish.venuePrice, dish.currency, locale),
        medianPrice: hasMedian
          ? this.currency.format(dish.medianPrice!, dish.currency, locale)
          : '—',
        delta: hasMedian ? this.currency.format(delta!, dish.currency, locale) : '—',
        position,
      };
    });
  });

  /** The accessible i18n label key describing a row's position relative to the market median. */
  positionLabelKey(position: PriceRow['position']): string {
    switch (position) {
      case 'cheaper':
        return 'portal.analytics.price.cheaper';
      case 'dearer':
        return 'portal.analytics.price.dearer';
      case 'equal':
        return 'portal.analytics.price.equal';
      default:
        return 'portal.analytics.price.noMedian';
    }
  }
}
