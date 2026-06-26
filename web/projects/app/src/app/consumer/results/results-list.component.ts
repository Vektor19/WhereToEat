import { ScrollingModule } from '@angular/cdk/scrolling';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import { Router } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';

import { TranslatePipe } from '@ngx-translate/core';

import {
  AnalyticsEmitterService,
  AnalyticsEventBuilders,
  CurrencyFormatter,
  DistanceFormatter,
  LocaleService,
  RatingPresenter,
  ResultsStore,
  type RecommendedRestaurantDto,
} from 'core';

import { LoadingComponent } from '../../shared/ui/loading.component';
import { ErrorStateComponent } from '../../shared/ui/error-state.component';
import { MapModalComponent } from '../map/map-modal.component';
import { AdSlotComponent } from './ad-slot.component';
import { ResultCardComponent } from './result-card.component';
import type { ResultCardViewModel } from './result-card.view-model';

/** The generic dish/category illustration — our own content, the default on every card (invariant #8). */
const GENERIC_PHOTO_SRC = 'generic-dish.svg';

/**
 * Above this many results the list switches to CDK virtual scrolling so a long ranked list does not
 * render every card up-front (design §Performance: "virtualize/lazy-render long result lists"). Below
 * it the list renders inline so short results stay simple and fully in the DOM for tests/SEO.
 */
const VIRTUALIZE_THRESHOLD = 20;

/** Estimated card height (px) for the virtual viewport's `itemSize` — tuned to the card's layout. */
const VIRTUAL_ITEM_SIZE = 132;

/**
 * Results-list container (smart) — renders the ranked recommendation from the {@link ResultsStore}.
 *
 * It is the only place that touches the store, the formatters/presenter (Step 8), and analytics; the
 * cards/ad-slot are pure presentational children, so the smart/dumb seam lines up with the
 * business-logic-vs-view boundary the RN rewrite mirrors.
 *
 * **Order is the backend's (invariant #5).** The store holds the engine's ranked, filtered list and is
 * never re-sorted client-side; this container maps it 1:1, preserving order, into
 * {@link ResultCardViewModel}s. Each card's basket price is currency-formatted by the DTO's
 * `basketPriceCurrency` (never a hardcoded symbol) **with no `≈`/per-card disclaimer** (invariant #12);
 * the rating is our smoothed presentation with the low-review note (invariant #6); distance shows only
 * when geo was provided (invariant #11); coverage is rendered secondary by the card.
 *
 * **Ad-slot contract (invariant #10).** A view-model with `isAd === true` is rendered through the
 * labeled, separated {@link AdSlotComponent}; organic results never get the ad chrome. The backend
 * recommend response has no ad field yet, so the real flow only ever produces organic (`isAd === false`)
 * view-models — the slot is the presentation contract the list is *structured* to honour.
 *
 * **States** reuse the shared loading/error/empty primitives (Step 7). On a successful load it emits
 * one `impression` event per shown card — deduped per result set through the Step 16 batching emitter
 * (best-effort, non-blocking) — and `card_open` on open. All analytics route through the single
 * {@link AnalyticsEmitterService} emission path; there is no direct ingest here.
 */
@Component({
  selector: 'app-results-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ScrollingModule,
    MatIconModule,
    TranslatePipe,
    LoadingComponent,
    ErrorStateComponent,
    ResultCardComponent,
    AdSlotComponent,
    MapModalComponent,
  ],
  template: `
    @if (loading()) {
      <app-loading [label]="'consumer.results.loading' | translate" />
    } @else if (error() !== undefined) {
      <app-error-state [error]="error()" [retryable]="false" />
    } @else if (empty()) {
      <app-error-state [emptyMessage]="'consumer.results.empty' | translate" />
    } @else if (cards().length > 0) {
      <section
        class="dp-results"
        [attr.aria-label]="'consumer.results.regionLabel' | translate"
        data-testid="results-list"
      >
        @if (virtualize()) {
          <cdk-virtual-scroll-viewport
            class="dp-results__viewport"
            [itemSize]="itemSize"
            data-testid="results-virtual"
          >
            <div *cdkVirtualFor="let vm of cards(); trackBy: trackById" class="dp-results__row">
              @if (vm.isAd) {
                <app-ad-slot [vm]="vm" (open)="onOpen($event)" (viewMap)="onViewMap($event)" />
              } @else {
                <app-result-card [vm]="vm" (open)="onOpen($event)" (viewMap)="onViewMap($event)" />
              }
            </div>
          </cdk-virtual-scroll-viewport>
        } @else {
          @for (vm of cards(); track vm.restaurantId) {
            <div class="dp-results__row">
              @if (vm.isAd) {
                <app-ad-slot [vm]="vm" (open)="onOpen($event)" (viewMap)="onViewMap($event)" />
              } @else {
                <app-result-card [vm]="vm" (open)="onOpen($event)" (viewMap)="onViewMap($event)" />
              }
            </div>
          }
        }
      </section>
    }

    @if (mapTarget(); as target) {
      <app-map-modal
        [restaurantId]="target.restaurantId"
        [name]="target.name"
        (closed)="onMapClose()"
      />
    }
  `,
  styles: `
    :host {
      display: block;
    }

    .dp-results {
      display: flex;
      flex-direction: column;
      gap: var(--dp-space-3);
    }

    .dp-results__viewport {
      block-size: 70vh;
    }

    .dp-results__row {
      padding-block-end: var(--dp-space-3);
    }
  `,
})
export class ResultsListComponent {
  private readonly results = inject(ResultsStore);
  private readonly currency = inject(CurrencyFormatter);
  private readonly distance = inject(DistanceFormatter);
  private readonly rating = inject(RatingPresenter);
  private readonly events = inject(AnalyticsEventBuilders);
  private readonly emitter = inject(AnalyticsEmitterService);
  private readonly router = inject(Router);
  private readonly locale = inject(LocaleService);

  /** The virtual viewport's per-item size hint. */
  readonly itemSize = VIRTUAL_ITEM_SIZE;

  // ── Bound store state ───────────────────────────────────────────────────────
  readonly loading = this.results.loadingResults;
  readonly error = this.results.error;
  readonly empty = this.results.empty;

  /** How many selected items the query carried (the coverage denominator "N of M"). */
  private readonly selectionSize = computed<number>(
    () => this.results.request()?.items.length ?? 0,
  );

  /**
   * The backend-ordered cards as resolved view-models (invariant #5 — never re-sorted). Each DTO is
   * mapped 1:1 through the formatters/presenter, preserving the list's order.
   */
  readonly cards = computed<readonly ResultCardViewModel[]>(() => {
    const total = this.selectionSize();
    // Read the active locale here so a language switch re-runs the formatters (currency/distance).
    const locale = this.locale.activeLocale();
    return this.results
      .restaurants()
      .map((dto, index) => this.toViewModel(dto, index + 1, total, locale));
  });

  /** Switch to virtual scrolling once the list is long enough to warrant it (performance). */
  readonly virtualize = computed<boolean>(() => this.cards().length > VIRTUALIZE_THRESHOLD);

  /** The restaurant whose inline map modal is open (§5.7), or `null` when no modal is shown. */
  readonly mapTarget = signal<ResultCardViewModel | null>(null);

  /** The element to return focus to when the map modal closes (WCAG focus management). */
  private mapInvoker: HTMLElement | null = null;

  constructor() {
    // Emit one impression per shown card whenever a successful result lands (best-effort).
    effect(() => {
      if (!this.results.loaded()) {
        return;
      }
      this.emitImpressions(this.cards());
    });
  }

  /** `trackBy` for the virtual-for (the restaurant id is stable). */
  trackById(_index: number, vm: ResultCardViewModel): string {
    return vm.restaurantId;
  }

  /**
   * Open a card: emit `card_open` then navigate to the restaurant-details route (Step 14). The
   * details surface also emits its own `view`/`card_open` on load; this card-open marks the
   * originating list interaction (with its list `position`) before the navigation.
   */
  onOpen(vm: ResultCardViewModel): void {
    this.emitter.emit(
      this.events.cardOpen({ restaurantId: vm.restaurantId, position: vm.position }),
    );
    void this.router.navigate(['restaurant', vm.restaurantId]);
  }

  /**
   * Open the inline "Глянути на карті" map modal for a card (§5.7) — the user stays in the list. The
   * modal fetches the live payload and emits the `action` analytics event itself; here we only record
   * the invoking element so focus returns to it on close (WCAG focus management).
   */
  onViewMap(vm: ResultCardViewModel): void {
    const active = document.activeElement;
    this.mapInvoker = active instanceof HTMLElement ? active : null;
    this.mapTarget.set(vm);
  }

  /** Close the map modal and return focus to the affordance that opened it (WCAG). */
  onMapClose(): void {
    this.mapTarget.set(null);
    this.mapInvoker?.focus();
    this.mapInvoker = null;
  }

  // ── Internals ───────────────────────────────────────────────────────────────

  /**
   * Map one ranked DTO to its card view-model. `isAd` defaults to `false` — the backend recommend
   * response has no ad-slot field yet, so the real flow only produces organic cards; the ad-slot is
   * the contract the list honours when the backend adds it (invariant #10).
   */
  private toViewModel(
    dto: RecommendedRestaurantDto,
    position: number,
    selectionTotal: number,
    locale: string,
  ): ResultCardViewModel {
    return {
      restaurantId: dto.restaurantId,
      name: dto.name,
      position,
      basketPrice: this.currency.formatOptional(
        dto.basketPriceAmount,
        dto.basketPriceCurrency,
        locale,
      ),
      rating: this.rating.present(dto.smoothedRating, dto.ratingCount, locale),
      distance: this.distance.formatOptionalKm(dto.distanceKm, locale),
      coverageCovered: dto.coverage,
      coverageTotal: selectionTotal,
      photo: { isReal: false, src: GENERIC_PHOTO_SRC, alt: dto.name },
      isAd: false,
    };
  }

  /**
   * Emit one `impression` event per shown card via the batching emitter, **deduped per result set**:
   * the emitter keys impressions by the result-set token + restaurant id, so the effect re-running on
   * change detection never double-counts a card. A new recommendation (new token) impresses afresh.
   */
  private emitImpressions(cards: readonly ResultCardViewModel[]): void {
    if (cards.length === 0) {
      return;
    }
    const impressions = cards.map((vm) =>
      this.events.impression({ restaurantId: vm.restaurantId, position: vm.position }),
    );
    this.emitter.emitImpressionsOnce(this.resultSetToken(), impressions);
  }

  /**
   * A stable identity for the current ranked result set, used as the impression-dedupe scope. Derived
   * from the request echo's result-set identity (items + match + sort + filters) — **`userGeo` is
   * deliberately omitted**: toggling geo opt-in on/off does not change *which* cards the set is, so it
   * must not open a fresh dedupe scope and re-impress the same cards (which would inflate impression
   * counts). The same query reuses one scope; a different query starts a fresh one.
   */
  private resultSetToken(): string {
    const request = this.results.request();
    if (request === undefined) {
      return 'none';
    }
    const { userGeo: _omitted, ...identity } = request;
    void _omitted;
    return JSON.stringify(identity);
  }
}
