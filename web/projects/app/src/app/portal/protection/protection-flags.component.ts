import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatIconModule } from '@angular/material/icon';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { TranslatePipe } from '@ngx-translate/core';

import {
  GET_RESTAURANT_DETAILS,
  LoadingService,
  SET_MENU_ITEM_DO_NOT_PARSE,
  SET_RESTAURANT_DO_NOT_UPDATE,
  type ApiError,
  type MenuItemDto,
  type RestaurantDetailsDto,
} from 'core';

import { LoadingComponent } from '../../shared/ui/loading.component';
import { ErrorStateComponent } from '../../shared/ui/error-state.component';
import { RestaurantPickerComponent } from '../shared/restaurant-picker.component';

/** The reactive state of one protection toggle's last write (per item / per restaurant). */
interface FlagState {
  readonly value: boolean;
  readonly saving: boolean;
  readonly error: ApiError | undefined;
}

const IDLE_FLAG: FlagState = { value: false, saving: false, error: undefined };

/**
 * Protection-flags container (smart) — Step 19. The "admin > parser" controls (invariant #3): the
 * per-menu-item **do-not-parse** flag (`PUT /admin/menu-items/{id}/do-not-parse`) the parser observes to
 * skip a position, and the restaurant-level **do-not-update** flag
 * (`PUT /admin/restaurants/{id}/do-not-update`) that stops the parser overwriting a hand-curated
 * venue. Both send the `{ value }` body and treat **204** as success.
 *
 * There is no admin read of the current flag values (no dedicated admin read endpoint), so a toggle is
 * an explicit **set** control: flipping it issues the write and the row shows in-progress / saved / typed
 * error for that write. The copy makes the parser-protection meaning explicit so an operator understands
 * what each flag guards. The restaurant is addressed by id through the shared picker; the menu items come
 * from the same public `GET /restaurants/{id}` read the consumer details use.
 *
 * This is the only place that touches the data-access ports; the picker is presentational. Portal code
 * never imports consumer internals; copy resolves from the runtime i18n catalogs; themed from tokens.
 */
@Component({
  selector: 'app-protection-flags',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    MatSlideToggleModule,
    MatIconModule,
    TranslatePipe,
    LoadingComponent,
    ErrorStateComponent,
    RestaurantPickerComponent,
  ],
  template: `
    <section class="dp-protect" data-testid="protection-flags">
      <header class="dp-protect__header">
        <h1 class="dp-protect__title">{{ 'portal.protection.title' | translate }}</h1>
        <p class="dp-protect__intro">{{ 'portal.protection.intro' | translate }}</p>
      </header>

      <app-restaurant-picker
        labelKey="portal.protection.restaurantLabel"
        [loading]="loading()"
        (loadId)="loadRestaurant($event)"
      />

      @if (loading()) {
        <app-loading />
      } @else if (error() !== undefined) {
        <app-error-state [error]="error()" [retryable]="true" (retry)="reload()" />
      } @else if (restaurant(); as r) {
        <!-- Restaurant-level do-not-update (protects a hand-curated venue from the parser). -->
        <div class="dp-protect__restaurant" data-testid="restaurant-do-not-update">
          <mat-slide-toggle
            [checked]="restaurantFlag().value"
            [disabled]="restaurantFlag().saving"
            data-testid="restaurant-do-not-update-toggle"
            (change)="setRestaurantDoNotUpdate(r.id, $event.checked)"
          >
            {{ 'portal.protection.doNotUpdate' | translate }}
          </mat-slide-toggle>
          <p class="dp-protect__hint">{{ 'portal.protection.doNotUpdateHint' | translate }}</p>
          @if (restaurantFlag().saving) {
            <span class="dp-protect__pending" role="status" data-testid="restaurant-flag-pending">{{
              'portal.save.saving' | translate
            }}</span>
          } @else if (restaurantFlag().error; as err) {
            <span
              class="dp-protect__error"
              role="alert"
              data-testid="restaurant-flag-error"
              [attr.data-message-key]="err.messageKey"
              >{{ err.messageKey | translate }}</span
            >
          }
        </div>

        <!-- Per-item do-not-parse (the parser skips that position). -->
        <h2 class="dp-protect__items-title">{{ 'portal.protection.itemsTitle' | translate }}</h2>
        <ul class="dp-protect__items">
          @for (item of r.menuItems; track item.id) {
            <li class="dp-protect__item" [attr.data-testid]="'item-' + item.id">
              <mat-slide-toggle
                [checked]="itemFlag(item.id).value"
                [disabled]="itemFlag(item.id).saving"
                [attr.data-testid]="'item-do-not-parse-' + item.id"
                (change)="setItemDoNotParse(item.id, $event.checked)"
              >
                {{ itemLabel(item) }}
              </mat-slide-toggle>
              @if (itemFlag(item.id).saving) {
                <span class="dp-protect__pending" role="status">{{
                  'portal.save.saving' | translate
                }}</span>
              } @else if (itemFlag(item.id).error; as err) {
                <span
                  class="dp-protect__error"
                  role="alert"
                  [attr.data-testid]="'item-flag-error-' + item.id"
                  [attr.data-message-key]="err.messageKey"
                  >{{ err.messageKey | translate }}</span
                >
              }
            </li>
          } @empty {
            <li class="dp-protect__empty" data-testid="protection-empty">
              {{ 'portal.protection.noItems' | translate }}
            </li>
          }
        </ul>
      }
    </section>
  `,
  styles: `
    :host {
      display: block;
    }

    .dp-protect {
      display: flex;
      flex-direction: column;
      gap: var(--dp-space-4);
    }

    .dp-protect__title {
      margin: 0;
      font-size: var(--dp-font-size-title);
      font-weight: var(--dp-font-weight-bold);
    }

    .dp-protect__intro {
      margin: var(--dp-space-1) 0 0;
      color: var(--dp-color-on-surface-variant);
    }

    .dp-protect__restaurant {
      display: flex;
      flex-direction: column;
      gap: var(--dp-space-1);
      padding: var(--dp-space-3);
      border: var(--dp-border-width-hairline) solid var(--dp-color-outline-variant);
      border-radius: var(--dp-radius-sm);
    }

    .dp-protect__items-title {
      margin: 0;
      font-size: var(--dp-font-size-subtitle);
      font-weight: var(--dp-font-weight-bold);
    }

    .dp-protect__items {
      list-style: none;
      margin: 0;
      padding: 0;
      display: flex;
      flex-direction: column;
      gap: var(--dp-space-2);
    }

    .dp-protect__item {
      display: flex;
      align-items: center;
      gap: var(--dp-space-3);
    }

    .dp-protect__hint {
      margin: 0;
      color: var(--dp-color-on-surface-variant);
      font-size: var(--dp-font-size-caption);
    }

    .dp-protect__pending {
      color: var(--dp-color-on-surface-variant);
      font-size: var(--dp-font-size-caption);
    }

    .dp-protect__error {
      color: var(--dp-color-error);
      font-size: var(--dp-font-size-caption);
    }

    .dp-protect__empty {
      color: var(--dp-color-on-surface-variant);
    }
  `,
})
export class ProtectionFlagsComponent {
  private readonly getDetails = inject(GET_RESTAURANT_DETAILS);
  private readonly setItemFlag = inject(SET_MENU_ITEM_DO_NOT_PARSE);
  private readonly setRestaurantFlag = inject(SET_RESTAURANT_DO_NOT_UPDATE);
  private readonly loadingService = inject(LoadingService);
  private readonly destroyRef = inject(DestroyRef);

  /** The loaded restaurant's details async-state (loading / error / loaded). */
  private readonly detailsState = this.loadingService.create<RestaurantDetailsDto>();
  private readonly restaurantId = signal<string | null>(null);

  /** Per-menu-item do-not-parse flag state, keyed by menu item id. */
  private readonly itemFlags = signal<ReadonlyMap<string, FlagState>>(new Map());
  /** The restaurant-level do-not-update flag state. */
  private readonly restaurantFlagState = signal<FlagState>(IDLE_FLAG);

  // ── Bound read async-state ────────────────────────────────────────────────
  readonly loading = this.detailsState.loading;
  readonly error = this.detailsState.error;
  readonly restaurant = this.detailsState.data;

  /** The restaurant-level do-not-update flag state (for the template). */
  readonly restaurantFlag = computed<FlagState>(() => this.restaurantFlagState());

  /** Fetch the restaurant's details (its menu items). Resets all flag state. */
  loadRestaurant(id: string): void {
    this.restaurantId.set(id);
    this.itemFlags.set(new Map());
    this.restaurantFlagState.set(IDLE_FLAG);
    this.detailsState.run(this.getDetails.execute(id));
  }

  /** Re-run the details fetch (the shared error-state retry affordance). */
  reload(): void {
    const id = this.restaurantId();
    if (id !== null) {
      this.detailsState.run(this.getDetails.execute(id));
    }
  }

  /** The flag state for one menu item (idle until first toggled). */
  itemFlag(menuItemId: string): FlagState {
    return this.itemFlags().get(menuItemId) ?? IDLE_FLAG;
  }

  /** Set a menu item's do-not-parse flag (`PUT .../do-not-parse` with `{ value }`); 204 → saved. */
  setItemDoNotParse(menuItemId: string, value: boolean): void {
    this.patchItemFlag(menuItemId, { value, saving: true, error: undefined });
    this.setItemFlag
      .execute(menuItemId, value)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => this.patchItemFlag(menuItemId, { value, saving: false, error: undefined }),
        error: (err: ApiError) =>
          this.patchItemFlag(menuItemId, { value: !value, saving: false, error: err }),
      });
  }

  /** Set the restaurant do-not-update flag (`PUT .../do-not-update` with `{ value }`); 204 → saved. */
  setRestaurantDoNotUpdate(restaurantId: string, value: boolean): void {
    this.restaurantFlagState.set({ value, saving: true, error: undefined });
    this.setRestaurantFlag
      .execute(restaurantId, value)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => this.restaurantFlagState.set({ value, saving: false, error: undefined }),
        error: (err: ApiError) =>
          this.restaurantFlagState.set({ value: !value, saving: false, error: err }),
      });
  }

  /** A stable per-item label: the dish id (a name lookup would need the taxonomy; id is unambiguous). */
  itemLabel(item: MenuItemDto): string {
    return item.dishId;
  }

  /** Commit a single item's flag state into the keyed map (a fresh map keeps the signal immutable). */
  private patchItemFlag(menuItemId: string, state: FlagState): void {
    this.itemFlags.update((map) => new Map(map).set(menuItemId, state));
  }
}
