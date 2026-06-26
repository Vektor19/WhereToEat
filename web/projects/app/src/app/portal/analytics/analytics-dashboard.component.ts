import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators, type FormGroup } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { TranslatePipe } from '@ngx-translate/core';

import {
  GET_DEMAND,
  GET_FUNNEL,
  GET_PRICE_POSITIONING,
  GET_RATINGS_DISTRIBUTION,
  GET_TRAFFIC,
  LoadingService,
  type ConversionFunnelDto,
  type DemandBreakdownDto,
  type PricePositioningDto,
  type RatingsDistributionDto,
  type RestaurantTrafficSummaryDto,
} from 'core';

import { LoadingComponent } from '../../shared/ui/loading.component';
import { ErrorStateComponent } from '../../shared/ui/error-state.component';
import { TrafficSummaryComponent } from './traffic-summary.component';
import { ConversionFunnelComponent } from './conversion-funnel.component';
import { DemandBreakdownComponent } from './demand-breakdown.component';
import { PricePositioningComponent } from './price-positioning.component';
import { RatingsDistributionComponent } from './ratings-distribution.component';

/** The default lookback window (days) the form seeds when the dashboard opens. */
const DEFAULT_WINDOW_DAYS = 30;

/**
 * Operator §7.5 analytics-dashboard container (smart) — Step 24.
 *
 * The operator picks a restaurant id and a time window, and the container fans out to the **admin-host,
 * admin-policy-gated, aggregates-only** read endpoints (Step 23) through the data-access ports — each
 * call rides the admin prefix, so the Step 6 bearer interceptor attaches the operator token and the
 * Admin host enforces the policy server-side. Every figure rendered is an aggregate the backend
 * returns; **nothing per-user/per-event ever crosses the boundary** (invariant #11 end-to-end — the
 * typed responses have no such field by shape).
 *
 * Five reads back the five metric families: traffic + funnel + demand are window-scoped (demand is
 * area-wide, not restaurant-scoped); price-positioning + ratings are restaurant-only (no window).
 * Each metric panel has its own shared async-state (loading / typed-error / loaded) so one slow or
 * failing query does not blank the others; the panels are dumb/presentational children, so the
 * smart/dumb seam lines up with the business-logic-vs-view boundary. Portal code never imports consumer
 * internals; copy resolves from the runtime i18n catalogs; styling is token-driven and responsive.
 */
@Component({
  selector: 'app-analytics-dashboard',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    TranslatePipe,
    LoadingComponent,
    ErrorStateComponent,
    TrafficSummaryComponent,
    ConversionFunnelComponent,
    DemandBreakdownComponent,
    PricePositioningComponent,
    RatingsDistributionComponent,
  ],
  template: `
    <section class="dp-dash" data-testid="analytics-dashboard">
      <header class="dp-dash__header">
        <h1 class="dp-dash__title">{{ 'portal.analytics.title' | translate }}</h1>
        <p class="dp-dash__intro">{{ 'portal.analytics.intro' | translate }}</p>
        <p class="dp-dash__privacy" data-testid="analytics-privacy">
          <mat-icon class="dp-dash__privacy-icon" aria-hidden="true">lock</mat-icon>
          <span>{{ 'portal.analytics.privacyNote' | translate }}</span>
        </p>
      </header>

      <form
        class="dp-dash__form"
        data-testid="analytics-form"
        [formGroup]="form"
        (submit)="load($event)"
      >
        <mat-form-field appearance="outline">
          <mat-label>{{ 'portal.analytics.restaurantLabel' | translate }}</mat-label>
          <mat-icon matIconPrefix aria-hidden="true">store</mat-icon>
          <input
            matInput
            type="text"
            autocomplete="off"
            formControlName="restaurantId"
            data-testid="analytics-restaurant-input"
          />
          <mat-hint>{{ 'portal.analytics.restaurantHint' | translate }}</mat-hint>
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>{{ 'portal.analytics.from' | translate }}</mat-label>
          <input
            matInput
            type="datetime-local"
            formControlName="from"
            data-testid="analytics-from-input"
          />
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>{{ 'portal.analytics.to' | translate }}</mat-label>
          <input
            matInput
            type="datetime-local"
            formControlName="to"
            data-testid="analytics-to-input"
          />
        </mat-form-field>

        <button
          mat-flat-button
          color="primary"
          type="submit"
          data-testid="analytics-load"
          [disabled]="form.invalid"
        >
          <mat-icon aria-hidden="true">query_stats</mat-icon>
          {{ 'portal.analytics.load' | translate }}
        </button>
      </form>

      @if (!hasLoaded()) {
        <p class="dp-dash__hint" data-testid="analytics-pre-load">
          {{ 'portal.analytics.preLoad' | translate }}
        </p>
      } @else {
        <div class="dp-dash__grid">
          <!-- Visibility / traffic -->
          @if (trafficLoading()) {
            <app-loading />
          } @else if (trafficError() !== undefined) {
            <app-error-state [error]="trafficError()" [retryable]="false" />
          } @else if (traffic(); as t) {
            <app-analytics-traffic [summary]="t" />
          }

          <!-- Conversion funnel -->
          @if (funnelLoading()) {
            <app-loading />
          } @else if (funnelError() !== undefined) {
            <app-error-state [error]="funnelError()" [retryable]="false" />
          } @else if (funnel(); as f) {
            <app-analytics-funnel [funnel]="f" />
          }

          <!-- Ratings distribution -->
          @if (ratingsLoading()) {
            <app-loading />
          } @else if (ratingsError() !== undefined) {
            <app-error-state [error]="ratingsError()" [retryable]="false" />
          } @else if (ratings(); as r) {
            <app-analytics-ratings [distribution]="r" />
          }

          <!-- Price positioning (full width) -->
          <div class="dp-dash__wide">
            @if (priceLoading()) {
              <app-loading />
            } @else if (priceError() !== undefined) {
              <app-error-state [error]="priceError()" [retryable]="false" />
            } @else if (price(); as p) {
              <app-analytics-price-positioning [positioning]="p" />
            }
          </div>

          <!-- Demand (full width) -->
          <div class="dp-dash__wide">
            @if (demandLoading()) {
              <app-loading />
            } @else if (demandError() !== undefined) {
              <app-error-state [error]="demandError()" [retryable]="false" />
            } @else if (demand(); as d) {
              <app-analytics-demand [breakdown]="d" />
            }
          </div>
        </div>
      }
    </section>
  `,
  styles: `
    :host {
      display: block;
    }

    .dp-dash {
      display: flex;
      flex-direction: column;
      gap: var(--dp-space-4);
    }

    .dp-dash__title {
      margin: 0;
      font-size: var(--dp-font-size-title);
      font-weight: var(--dp-font-weight-bold);
    }

    .dp-dash__intro {
      margin: var(--dp-space-1) 0 0;
      color: var(--dp-color-on-surface-variant);
    }

    .dp-dash__privacy {
      display: flex;
      align-items: flex-start;
      gap: var(--dp-space-2);
      margin: var(--dp-space-2) 0 0;
      font-size: var(--dp-font-size-caption);
      color: var(--dp-color-on-surface-variant);
    }

    .dp-dash__privacy-icon {
      flex: none;
      font-size: var(--dp-font-size-body);
      width: var(--dp-font-size-body);
      height: var(--dp-font-size-body);
    }

    .dp-dash__form {
      display: flex;
      gap: var(--dp-space-3);
      flex-wrap: wrap;
      align-items: flex-start;
    }

    .dp-dash__hint {
      margin: 0;
      color: var(--dp-color-on-surface-variant);
    }

    .dp-dash__grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(var(--dp-column-min-width-lg), 1fr));
      gap: var(--dp-space-4);
      align-items: start;
    }

    .dp-dash__wide {
      grid-column: 1 / -1;
    }
  `,
})
export class AnalyticsDashboardComponent {
  private readonly getTraffic = inject(GET_TRAFFIC);
  private readonly getFunnel = inject(GET_FUNNEL);
  private readonly getDemand = inject(GET_DEMAND);
  private readonly getPrice = inject(GET_PRICE_POSITIONING);
  private readonly getRatings = inject(GET_RATINGS_DISTRIBUTION);
  private readonly loadingService = inject(LoadingService);
  private readonly fb = inject(FormBuilder);

  // ── One shared async-state per metric family (independent loading/error) ──
  private readonly trafficState = this.loadingService.create<RestaurantTrafficSummaryDto>();
  private readonly funnelState = this.loadingService.create<ConversionFunnelDto>();
  private readonly demandState = this.loadingService.create<DemandBreakdownDto>();
  private readonly priceState = this.loadingService.create<PricePositioningDto>();
  private readonly ratingsState = this.loadingService.create<RatingsDistributionDto>();

  /** True once the operator has run at least one load (drives the pre-load hint vs the grid). */
  private readonly loaded = signal(false);
  readonly hasLoaded = computed(() => this.loaded());

  // ── Bound metric signals for the template ─────────────────────────────────
  readonly traffic = this.trafficState.data;
  readonly trafficLoading = this.trafficState.loading;
  readonly trafficError = this.trafficState.error;

  readonly funnel = this.funnelState.data;
  readonly funnelLoading = this.funnelState.loading;
  readonly funnelError = this.funnelState.error;

  readonly demand = this.demandState.data;
  readonly demandLoading = this.demandState.loading;
  readonly demandError = this.demandState.error;

  readonly price = this.priceState.data;
  readonly priceLoading = this.priceState.loading;
  readonly priceError = this.priceState.error;

  readonly ratings = this.ratingsState.data;
  readonly ratingsLoading = this.ratingsState.loading;
  readonly ratingsError = this.ratingsState.error;

  /** Restaurant id + the from/to window (datetime-local strings), seeded to the last 30 days. */
  readonly form: FormGroup = this.fb.group({
    restaurantId: ['', Validators.required],
    from: [this.defaultFrom(), Validators.required],
    to: [this.defaultTo(), Validators.required],
  });

  /** Fan out to all five aggregate reads for the chosen restaurant + window. */
  load(event: Event): void {
    event.preventDefault();
    if (this.form.invalid) {
      return;
    }
    const raw = this.form.getRawValue() as { restaurantId: string; from: string; to: string };
    const restaurantId = raw.restaurantId.trim();
    if (restaurantId.length === 0) {
      return;
    }
    // The form's datetime-local values are local wall-clock; convert to ISO instants for the API.
    const from = new Date(raw.from).toISOString();
    const to = new Date(raw.to).toISOString();

    this.loaded.set(true);
    this.trafficState.run(this.getTraffic.execute(restaurantId, from, to));
    this.funnelState.run(this.getFunnel.execute(restaurantId, from, to));
    this.demandState.run(this.getDemand.execute(from, to));
    this.priceState.run(this.getPrice.execute(restaurantId));
    this.ratingsState.run(this.getRatings.execute(restaurantId));
  }

  /** The `datetime-local` value for "now minus the default window" (no timezone suffix). */
  private defaultFrom(): string {
    const d = new Date();
    d.setDate(d.getDate() - DEFAULT_WINDOW_DAYS);
    return toLocalInput(d);
  }

  /** The `datetime-local` value for "now". */
  private defaultTo(): string {
    return toLocalInput(new Date());
  }
}

/** Format a `Date` as a `datetime-local` input value (`YYYY-MM-DDTHH:mm`) in local time. */
function toLocalInput(date: Date): string {
  const pad = (n: number): string => `${n}`.padStart(2, '0');
  return (
    `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}` +
    `T${pad(date.getHours())}:${pad(date.getMinutes())}`
  );
}
