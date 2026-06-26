import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { TranslatePipe } from '@ngx-translate/core';

import type { DemandBreakdownDto } from 'core';

/**
 * Demand-by-category/dish metric panel (dumb / presentational) — Step 24 (§7.5).
 *
 * Renders the most-searched categories and dishes in the window as two accessible tables (counts per
 * non-identifying selection id). A "selection id" identifies a taxonomy item (invariant #2), **never a
 * person**; the breakdown is the area-wide demand signal — what users search for, even items a venue
 * does not carry — and is **aggregates only** (invariant #11): the input rows carry a selection id and
 * a count and nothing else.
 *
 * Tables (rather than a chart) are the DRY, screen-reader-native choice for ranked counts: a
 * `<caption>` names each table and `scope="col"` headers label the columns.
 */
@Component({
  selector: 'app-analytics-demand',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatIconModule, TranslatePipe],
  template: `
    <section class="dp-metric" data-testid="analytics-demand" aria-labelledby="dp-demand-heading">
      <h2 id="dp-demand-heading" class="dp-metric__title">
        <mat-icon class="dp-metric__title-icon" aria-hidden="true">trending_up</mat-icon>
        {{ 'portal.analytics.demand.title' | translate }}
      </h2>
      <p class="dp-metric__note">{{ 'portal.analytics.demand.note' | translate }}</p>

      <div class="dp-demand__tables">
        <div class="dp-demand__table" data-testid="demand-categories">
          <h3 class="dp-demand__subtitle">
            {{ 'portal.analytics.demand.categories' | translate }}
          </h3>
          @if (breakdown().categories.length > 0) {
            <table>
              <caption class="dp-visually-hidden">
                {{
                  'portal.analytics.demand.categories' | translate
                }}
              </caption>
              <thead>
                <tr>
                  <th scope="col">{{ 'portal.analytics.demand.selection' | translate }}</th>
                  <th scope="col" class="dp-demand__count-col">
                    {{ 'portal.analytics.demand.searches' | translate }}
                  </th>
                </tr>
              </thead>
              <tbody>
                @for (row of breakdown().categories; track row.selectionId) {
                  <tr [attr.data-testid]="'demand-category-' + row.selectionId">
                    <td>{{ row.selectionId }}</td>
                    <td class="dp-demand__count-col">{{ row.searchCount }}</td>
                  </tr>
                }
              </tbody>
            </table>
          } @else {
            <p class="dp-metric__empty" data-testid="demand-categories-empty">
              {{ 'portal.analytics.demand.empty' | translate }}
            </p>
          }
        </div>

        <div class="dp-demand__table" data-testid="demand-dishes">
          <h3 class="dp-demand__subtitle">{{ 'portal.analytics.demand.dishes' | translate }}</h3>
          @if (breakdown().dishes.length > 0) {
            <table>
              <caption class="dp-visually-hidden">
                {{
                  'portal.analytics.demand.dishes' | translate
                }}
              </caption>
              <thead>
                <tr>
                  <th scope="col">{{ 'portal.analytics.demand.selection' | translate }}</th>
                  <th scope="col" class="dp-demand__count-col">
                    {{ 'portal.analytics.demand.searches' | translate }}
                  </th>
                </tr>
              </thead>
              <tbody>
                @for (row of breakdown().dishes; track row.selectionId) {
                  <tr [attr.data-testid]="'demand-dish-' + row.selectionId">
                    <td>{{ row.selectionId }}</td>
                    <td class="dp-demand__count-col">{{ row.searchCount }}</td>
                  </tr>
                }
              </tbody>
            </table>
          } @else {
            <p class="dp-metric__empty" data-testid="demand-dishes-empty">
              {{ 'portal.analytics.demand.empty' | translate }}
            </p>
          }
        </div>
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

    .dp-metric__note {
      margin: 0;
      font-size: var(--dp-font-size-caption);
      color: var(--dp-color-on-surface-variant);
    }

    .dp-demand__tables {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(var(--dp-column-min-width-md), 1fr));
      gap: var(--dp-space-4);
    }

    .dp-demand__subtitle {
      margin: 0 0 var(--dp-space-2);
      font-size: var(--dp-font-size-body);
      font-weight: var(--dp-font-weight-bold);
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

    .dp-demand__count-col {
      text-align: end;
      white-space: nowrap;
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
export class DemandBreakdownComponent {
  /** The aggregate demand breakdown for the selected window (area-wide). */
  readonly breakdown = input.required<DemandBreakdownDto>();
}
