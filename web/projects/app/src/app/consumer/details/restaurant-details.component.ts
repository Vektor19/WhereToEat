import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  signal,
} from '@angular/core';
import { Location } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { TranslateService } from '@ngx-translate/core';

import {
  AnalyticsEmitterService,
  AnalyticsEventBuilders,
  CatalogStore,
  CurrencyFormatter,
  GET_RESTAURANT_DETAILS,
  LoadingService,
  LocaleService,
  translateText,
  type ContactLinkDto,
  type MenuItemDto,
  type RestaurantDetailsDto,
} from 'core';

import { LoadingComponent } from '../../shared/ui/loading.component';
import { ErrorStateComponent } from '../../shared/ui/error-state.component';
import { ContactLinksComponent } from './contact-links.component';
import { MenuListComponent } from './menu-list.component';
import { RatingSubmitComponent } from '../rating/rating-submit.component';
import type {
  ContactLinkViewModel,
  MenuItemViewModel,
  RestaurantDetailsViewModel,
} from './details.view-model';

/** The generic dish/category illustration — our own content, the default on every menu item (invariant #8). */
const GENERIC_PHOTO_SRC = 'generic-dish.svg';

/**
 * Restaurant-details container (smart) — §5.8, design decision (always-free contact links).
 *
 * The routed details surface for `/restaurant/:id`. It fetches the **live** `GET /restaurants/{id}`
 * payload via {@link GET_RESTAURANT_DETAILS} through the shared async-state primitive (Step 7) so
 * loading / error / **404 not-found** all reuse the shared primitives. It is the only place that
 * touches the data-access port, the formatters (Step 8), the catalog lookup, and analytics; the
 * contact-links and menu-list children are pure presentational, so the smart/dumb seam lines up with
 * the business-logic-vs-view boundary the RN rewrite mirrors.
 *
 * Invariants honoured here:
 * - **Always-free contact links (§5.8 / invariant #10).** The contact links are rendered for **every**
 *   venue regardless of Verified status, with no paywall — the read model carries them unconditionally
 *   and this view never gates them.
 * - **Currency by `priceCurrency` (invariant §3).** Each menu price is formatted by the item's own
 *   `priceCurrency` via {@link CurrencyFormatter}, never a hardcoded symbol.
 * - **Generic photos by default (invariant #8).** Menu items render the generic asset; the read model
 *   has no real-photo permission signal today, so every item is generic.
 * - **No per-item disclaimer / no "updated X days ago" (invariant #12 / §10).** Neither the menu nor
 *   the page shows a per-item price/photo disclaimer or an "updated" stamp — the single discreet
 *   footer/`ⓘ` notice (Step 2) is the sole data-accuracy notice.
 *
 * On a successful load it emits one `view` and one `card_open` event through the single batching
 * emitter (best-effort, non-blocking), and an `action` event on a contact-link click (§5.9).
 */
@Component({
  selector: 'app-restaurant-details',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    MatButtonModule,
    MatIconModule,
    LoadingComponent,
    ErrorStateComponent,
    ContactLinksComponent,
    MenuListComponent,
    RatingSubmitComponent,
  ],
  template: `
    @if (loading()) {
      <app-loading [label]="loadingLabel()" />
    } @else if (error() !== undefined) {
      <app-error-state [error]="error()" [retryable]="true" (retry)="reload()" />
    } @else if (details(); as vm) {
      <article class="dp-details" data-testid="restaurant-details">
        <header class="dp-details__header">
          <button
            mat-stroked-button
            type="button"
            class="dp-details__back"
            data-testid="details-back"
            [attr.aria-label]="backLabel()"
            (click)="goBack()"
          >
            <mat-icon aria-hidden="true">arrow_back</mat-icon>
            {{ backLabel() }}
          </button>

          <h1 class="dp-details__name" data-testid="details-name">{{ vm.name }}</h1>
          <p class="dp-details__address" data-testid="details-address">{{ addressLabel(vm) }}</p>
        </header>

        <app-contact-links [links]="vm.contactLinks" (linkClick)="onContactClick()" />

        <app-menu-list [items]="vm.menuItems" />

        <app-rating-submit [restaurantId]="vm.restaurantId" />
      </article>
    } @else {
      <app-error-state [emptyMessage]="emptyLabel()" [retryable]="false" />
    }
  `,
  styles: `
    :host {
      display: block;
    }

    .dp-details {
      display: flex;
      flex-direction: column;
      gap: var(--dp-space-5);
      padding: var(--dp-space-4);
    }

    .dp-details__header {
      display: flex;
      flex-direction: column;
      gap: var(--dp-space-2);
    }

    .dp-details__back {
      align-self: flex-start;
    }

    .dp-details__name {
      margin: 0;
      font-size: var(--dp-font-size-title);
      font-weight: var(--dp-font-weight-bold);
    }

    .dp-details__address {
      margin: 0;
      font-size: var(--dp-font-size-body);
      color: var(--dp-color-on-surface-variant);
    }
  `,
})
export class RestaurantDetailsComponent {
  private readonly getDetails = inject(GET_RESTAURANT_DETAILS);
  private readonly currency = inject(CurrencyFormatter);
  private readonly catalog = inject(CatalogStore);
  private readonly events = inject(AnalyticsEventBuilders);
  private readonly emitter = inject(AnalyticsEmitterService);
  private readonly loadingService = inject(LoadingService);
  private readonly location = inject(Location);
  private readonly translate = inject(TranslateService);
  private readonly locale = inject(LocaleService);

  /**
   * The restaurant id from the route (`/restaurant/:id`). Bound via `withComponentInputBinding` so the
   * router param flows straight into the input — the container re-fetches whenever it changes.
   */
  readonly id = input.required<string>();

  // ── Copy (runtime i18n keys, Step 17) — reactive so a language switch re-resolves them. ──
  readonly loadingLabel = translateText(this.translate, 'consumer.details.loading');
  readonly emptyLabel = translateText(this.translate, 'consumer.details.empty');
  readonly backLabel = translateText(this.translate, 'common.back');

  /** The fetched live details payload's async state (loading / error / loaded), via the shared primitive. */
  private readonly state = this.loadingService.create<RestaurantDetailsDto>();

  // ── Bound async state ────────────────────────────────────────────────────────
  readonly loading = this.state.loading;
  readonly error = this.state.error;

  /** The resolved details view-model, or `null` until the payload lands. */
  readonly details = computed<RestaurantDetailsViewModel | null>(() => {
    const dto = this.state.data();
    // Read the active locale here so a language switch re-formats the menu prices.
    const locale = this.locale.activeLocale();
    return dto === undefined ? null : this.toViewModel(dto, locale);
  });

  /** Marks whether the `view`/`card_open` events have fired for the current load (once per id). */
  private readonly viewEmitted = signal(false);

  constructor() {
    // (Re)fetch whenever the route id changes, and emit `view`/`card_open` once per details open.
    effect(() => {
      const id = this.id();
      this.viewEmitted.set(false);
      this.fetch(id);
    });

    // Emit the view/card_open pair once the payload successfully lands (best-effort, non-blocking).
    effect(() => {
      if (!this.state.loaded()) {
        return;
      }
      if (this.viewEmitted()) {
        return;
      }
      this.viewEmitted.set(true);
      const id = this.id();
      this.emitter.emit(
        this.events.view({ restaurantId: id }),
        this.events.cardOpen({ restaurantId: id }),
      );
    });
  }

  /** Re-run the details fetch (the shared error-state retry affordance). */
  reload(): void {
    this.fetch(this.id());
  }

  /** Navigate back to the previous surface (the results list the user opened this card from). */
  goBack(): void {
    this.location.back();
  }

  /**
   * Fire the `action` analytics event on a contact-link click (§5.9). The link itself still navigates
   * (the anchor is not prevented) — this only records the interaction, best-effort and non-blocking.
   * The clicked link's identity is not needed (the event keys on the restaurant), so no arg is taken.
   */
  onContactClick(): void {
    this.emitter.emit(this.events.action({ restaurantId: this.id() }));
  }

  /** The composed "address, city" label (omits the city when the backend supplied none). */
  addressLabel(vm: RestaurantDetailsViewModel): string {
    return vm.addressCity !== null ? `${vm.addressLine}, ${vm.addressCity}` : vm.addressLine;
  }

  // ── Internals ───────────────────────────────────────────────────────────────

  private fetch(restaurantId: string): void {
    this.state.run(this.getDetails.execute(restaurantId));
  }

  /** Map the live DTO to the fully-resolved view-model (currency-formatted, name-resolved, generic photos). */
  private toViewModel(dto: RestaurantDetailsDto, locale: string): RestaurantDetailsViewModel {
    return {
      restaurantId: dto.id,
      name: dto.name,
      addressLine: dto.addressLine,
      addressCity: dto.addressCity ?? null,
      contactLinks: dto.contactLinks.map((link) => this.toContactLink(link)),
      menuItems: dto.menuItems.map((item) => this.toMenuItem(item, locale)),
    };
  }

  /** Map a contact-link DTO to its view-model: resolve the visible text and the `tel:` flag. */
  private toContactLink(link: ContactLinkDto): ContactLinkViewModel {
    const isPhone = link.url.startsWith('tel:') || link.kind.toLowerCase() === 'phone';
    return {
      kind: link.kind,
      url: link.url,
      // `??` only guards null/undefined; an empty/whitespace-only `label` must also fall back so the
      // visible text — and the `aria-label` derived from it — is never empty (WCAG 4.1.2).
      text: link.label?.trim().length ? link.label : this.fallbackLinkText(link),
      isPhone,
    };
  }

  /** A visible label when the backend supplied none: the kind, else the URL. */
  private fallbackLinkText(link: ContactLinkDto): string {
    const kind = link.kind.trim();
    return kind.length > 0 ? kind : link.url;
  }

  /**
   * Map a menu-item DTO to its view-model: resolve the dish name from the catalog (a neutral fallback
   * when the taxonomy is not loaded), format the price by the item's `priceCurrency` (never a hardcoded
   * symbol — invariant §3), and attach the generic-by-default photo (invariant #8).
   */
  private toMenuItem(item: MenuItemDto, locale: string): MenuItemViewModel {
    const dishName = this.resolveDishName(item.dishId);
    return {
      id: item.id,
      dishName,
      price: this.currency.format(item.priceAmount, item.priceCurrency, locale),
      weight: item.weight ?? null,
      photo: { isReal: false, src: GENERIC_PHOTO_SRC, alt: dishName },
    };
  }

  /**
   * Resolve a dish's canonical name from the **already-loaded** catalog taxonomy, falling back to the
   * neutral label when no loaded category holds it. Reads `this.catalog.state()` directly (a plain
   * lookup, no `computed` wrapping) so it still tracks the store state inside the outer `details`
   * computed but allocates no per-item `ComputedSignal` — `toMenuItem` runs once per item per
   * re-evaluation, so a fresh signal per item would be wasteful. Mirrors the fetch-free scan in
   * `CatalogStore.dishNameById` (kept on the store for other callers).
   */
  private resolveDishName(dishId: string): string {
    for (const state of this.catalog.state().dishesByCategory.values()) {
      const dish = state.data()?.find((d) => d.id === dishId);
      if (dish !== undefined) {
        return dish.canonicalName;
      }
    }
    // Neutral catalog-keyed label when the taxonomy has not loaded the dish (avoids a full fetch).
    // `instant` is correct: the catalogs are bundled in-memory (Step 17), so no async round-trip.
    return this.translate.instant('consumer.details.dishFallback');
  }
}
