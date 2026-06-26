import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { TranslatePipe } from '@ngx-translate/core';

import {
  AnalyticsEmitterService,
  AnalyticsEventBuilders,
  ResultsStore,
  SelectionStore,
  type FilterKey,
  type MatchMode,
  type SortMode,
  type ValidationError,
} from 'core';

import { GeoOptInComponent } from '../geo/geo-optin.component';
import { FiltersControlComponent } from './filters-control.component';
import { MatchControlComponent } from './match-control.component';
import { SortControlComponent } from './sort-control.component';

/**
 * Query-controls container (smart) — the second half of the deterministic consumer query (after the
 * Step 10 selection builder). It hosts the **match (Or/And)**, **sort** (the five pinned contract
 * modes), and **composable filters** (price max / rating min) controls and owns the **recommend
 * submit**.
 *
 * It is the only place that touches the {@link SelectionStore} (the query state) and the
 * {@link ResultsStore} (the submit + result lifecycle); the three controls are pure presentational
 * children, so the smart/dumb seam lines up with the business-logic-vs-view boundary the RN rewrite
 * mirrors. Changing a control routes straight to a store mutator, so the next built request reflects
 * the change.
 *
 * **Submit fails fast (invariant #1 — deterministic).** It builds the request through
 * {@link SelectionStore.buildRequest} (the Step 8 {@link RecommendRequestBuilder.tryBuild}, which
 * validates exactly-one-id-per-item, non-empty selection, known keys, geo range). An invalid build is
 * **not** sent — the typed {@link ValidationError}s are surfaced and submit stays disabled on an empty
 * selection. A valid build is submitted through the results store, which clears the previous result,
 * flips to loading, and lands on the ordered result or a typed error (rendered by Step 12). Only the
 * explicit selection + contract keys reach the engine — no free text.
 *
 * On a filter apply/clear it emits a `filter` analytics event through the single batching emitter
 * (best-effort, non-blocking).
 */
@Component({
  selector: 'app-query-controls',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatchControlComponent,
    SortControlComponent,
    FiltersControlComponent,
    GeoOptInComponent,
    TranslatePipe,
  ],
  template: `
    <mat-card class="dp-query" appearance="outlined" data-testid="query-controls">
      <app-match-control [value]="match()" (modeChange)="onMatch($event)" />

      <app-sort-control [value]="sort()" (modeChange)="onSort($event)" />

      <app-geo-optin />

      <app-filters-control
        [applied]="filters()"
        (apply)="onApplyFilter($event)"
        (clear)="onClearFilter($event)"
      />

      @if (validationErrors().length > 0) {
        <div class="dp-query__errors" role="alert" data-testid="query-validation">
          <mat-icon class="dp-query__errors-icon" aria-hidden="true">error_outline</mat-icon>
          <ul class="dp-query__errors-list">
            @for (error of validationErrors(); track error.code) {
              <li [attr.data-testid]="'query-error-' + error.code">{{ error.message }}</li>
            }
          </ul>
        </div>
      }

      <div class="dp-query__actions">
        <button
          mat-flat-button
          color="primary"
          type="button"
          class="dp-query__submit"
          data-testid="query-submit"
          [disabled]="submitDisabled()"
          (click)="onSubmit()"
        >
          <mat-icon aria-hidden="true">search</mat-icon>
          {{ 'consumer.query.submit' | translate }}
        </button>
      </div>
    </mat-card>
  `,
  styles: `
    .dp-query {
      display: flex;
      flex-direction: column;
      gap: var(--dp-space-5);
      padding: var(--dp-space-5);
    }

    .dp-query__errors {
      display: flex;
      align-items: flex-start;
      gap: var(--dp-space-2);
      padding: var(--dp-space-3);
      border: var(--dp-border-width-hairline) solid var(--dp-color-error);
      border-radius: var(--dp-radius-md);
      color: var(--dp-color-error);
    }

    .dp-query__errors-list {
      margin: 0;
      padding-inline-start: var(--dp-space-4);
      font-size: var(--dp-font-size-caption);
    }

    .dp-query__actions {
      display: flex;
      justify-content: flex-end;
    }
  `,
})
export class QueryControlsComponent {
  private readonly selection = inject(SelectionStore);
  private readonly results = inject(ResultsStore);
  private readonly events = inject(AnalyticsEventBuilders);
  private readonly emitter = inject(AnalyticsEmitterService);

  // ── Bound store state ───────────────────────────────────────────────────────
  readonly match = this.selection.match;
  readonly sort = this.selection.sort;
  readonly filters = this.selection.filters;
  readonly loading = this.results.loadingResults;

  /** Validation problems from the last submit attempt; cleared once the next build is valid. */
  private readonly validationErrorsState = signal<readonly ValidationError[]>([]);
  readonly validationErrors = computed<readonly ValidationError[]>(() =>
    this.validationErrorsState(),
  );

  /**
   * Submit is disabled while there is no selection (nothing to recommend) or a request is in flight.
   * An empty selection can never produce a valid build, so blocking it here keeps the deterministic
   * pipeline from ever receiving an empty request (mirrors the backend's non-empty rule).
   */
  readonly submitDisabled = computed<boolean>(
    () => !this.selection.hasSelection() || this.loading(),
  );

  // ── Control handlers ────────────────────────────────────────────────────────

  onMatch(match: MatchMode): void {
    this.selection.setMatch(match);
  }

  onSort(sort: SortMode): void {
    this.selection.setSort(sort);
  }

  /** Apply a filter value to the store and emit a `filter` analytics event. */
  onApplyFilter(change: { readonly key: FilterKey; readonly value: string }): void {
    this.selection.setFilter(change.key, change.value);
    this.emitFilter();
  }

  /** Drop a filter from the store and emit a `filter` analytics event (the active set changed). */
  onClearFilter(key: FilterKey): void {
    this.selection.removeFilter(key);
    this.emitFilter();
  }

  // ── Submit ──────────────────────────────────────────────────────────────────

  /**
   * Build + validate the request and, only when valid, submit it through the results store. An
   * invalid build never reaches the network — the typed errors are shown instead; a valid build clears
   * any stale validation messages and runs the request.
   */
  onSubmit(): void {
    const build = this.selection.buildRequest();
    if (!build.valid) {
      this.validationErrorsState.set(build.errors);
      return;
    }
    this.validationErrorsState.set([]);
    this.results.submit(build.request);
  }

  // ── Internals ───────────────────────────────────────────────────────────────

  /**
   * Emit a `filter` analytics event for the current applied filter keys through the single batching
   * emitter (best-effort, non-blocking — invariant #11: opaque session id, no PII; the emitter
   * swallows any ingest failure so the user flow never breaks on analytics).
   */
  private emitFilter(): void {
    this.emitter.emit(
      this.events.filter({
        filters: this.filters().map((f) => f.key),
        sortMode: this.sort(),
      }),
    );
  }
}
