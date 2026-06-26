import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { TranslatePipe } from '@ngx-translate/core';

import {
  CatalogStore,
  EDIT_MENU_ITEM,
  GET_RESTAURANT_DETAILS,
  LoadingService,
  type DishDto,
  type EditMenuItemBody,
  type MenuItemDto,
  type RestaurantDetailsDto,
} from 'core';

import { LoadingComponent } from '../../shared/ui/loading.component';
import { ErrorStateComponent } from '../../shared/ui/error-state.component';
import { RestaurantPickerComponent } from '../shared/restaurant-picker.component';
import { SaveFeedbackComponent } from '../shared/save-feedback.component';
import { MenuEditFormComponent, type MenuEditSeed } from './menu-edit-form.component';

/**
 * Menu-management container (smart) — Step 19. Admin is the source of truth over the parser
 * (invariant #3): an operator edits a menu item's price/weight and **re-categorises** it (changes the
 * dish through the Category → Dish taxonomy), persisting via `PUT /admin/menu-items/{id}`.
 *
 * There is no dedicated admin read endpoint, so the operator addresses a restaurant by id through the
 * shared {@link RestaurantPickerComponent}; the container fetches `GET /restaurants/{id}` (the same
 * public read the consumer details use) through the shared async-state primitive (Step 7) to list its
 * menu items, then loads the taxonomy from {@link CatalogStore} for the re-categorisation picker. On a
 * successful **204** it surfaces the saved state through the shared {@link SaveFeedbackComponent}; a
 * 404/400 surfaces as the typed {@link ApiError} the error interceptor produced.
 *
 * It is the only place that touches the data-access ports and the catalog store; the picker, edit form
 * and feedback strip are pure presentational children, so the smart/dumb seam lines up with the
 * business-logic-vs-view boundary the RN rewrite mirrors. Portal code never imports consumer internals.
 */
@Component({
  selector: 'app-menu-management',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    MatListModule,
    MatButtonModule,
    MatIconModule,
    TranslatePipe,
    LoadingComponent,
    ErrorStateComponent,
    RestaurantPickerComponent,
    SaveFeedbackComponent,
    MenuEditFormComponent,
  ],
  template: `
    <section class="dp-menu" data-testid="menu-management">
      <header class="dp-menu__header">
        <h1 class="dp-menu__title">{{ 'portal.menu.title' | translate }}</h1>
        <p class="dp-menu__intro">{{ 'portal.menu.intro' | translate }}</p>
      </header>

      <app-restaurant-picker
        labelKey="portal.menu.restaurantLabel"
        [loading]="loading()"
        (loadId)="loadRestaurant($event)"
      />

      @if (loading()) {
        <app-loading />
      } @else if (error() !== undefined) {
        <app-error-state [error]="error()" [retryable]="true" (retry)="reload()" />
      } @else if (restaurant(); as r) {
        <div class="dp-menu__body">
          <mat-nav-list
            class="dp-menu__items"
            [attr.aria-label]="'portal.menu.itemsLabel' | translate"
          >
            @for (item of r.menuItems; track item.id) {
              <a
                mat-list-item
                href="#"
                [attr.data-testid]="'menu-item-' + item.id"
                [class.dp-menu__item--active]="item.id === selectedId()"
                (click)="selectItem(item, $event)"
              >
                <span matListItemTitle>{{ dishName(item.dishId) }}</span>
                <span matListItemLine>{{ item.priceAmount }} {{ item.priceCurrency }}</span>
              </a>
            } @empty {
              <p class="dp-menu__empty" data-testid="menu-empty">
                {{ 'portal.menu.noItems' | translate }}
              </p>
            }
          </mat-nav-list>

          @if (seed(); as s) {
            <div class="dp-menu__editor">
              <app-menu-edit-form
                [categories]="categories()"
                [dishes]="editorDishes()"
                [dishesLoading]="editorDishesLoading()"
                [seed]="s"
                [saving]="saving()"
                (categoryChange)="loadCategoryDishes($event)"
                (save)="save($event)"
              />
              <app-save-feedback [saving]="saving()" [saved]="saved()" [error]="saveError()" />
            </div>
          }
        </div>
      }
    </section>
  `,
  styles: `
    :host {
      display: block;
    }

    .dp-menu {
      display: flex;
      flex-direction: column;
      gap: var(--dp-space-4);
    }

    .dp-menu__title {
      margin: 0;
      font-size: var(--dp-font-size-title);
      font-weight: var(--dp-font-weight-bold);
    }

    .dp-menu__intro {
      margin: var(--dp-space-1) 0 0;
      color: var(--dp-color-on-surface-variant);
    }

    .dp-menu__body {
      display: flex;
      gap: var(--dp-space-5);
      flex-wrap: wrap;
      align-items: flex-start;
    }

    .dp-menu__items {
      flex: 1 1 var(--dp-field-basis-lg);
      min-width: var(--dp-field-basis-md);
    }

    .dp-menu__item--active {
      background-color: var(--dp-color-surface-variant);
    }

    .dp-menu__editor {
      flex: 2 1 var(--dp-field-basis-xl);
      display: flex;
      flex-direction: column;
      gap: var(--dp-space-3);
    }

    .dp-menu__empty {
      margin: var(--dp-space-3);
      color: var(--dp-color-on-surface-variant);
    }
  `,
})
export class MenuManagementComponent {
  private readonly getDetails = inject(GET_RESTAURANT_DETAILS);
  private readonly editMenuItem = inject(EDIT_MENU_ITEM);
  private readonly catalog = inject(CatalogStore);
  private readonly loadingService = inject(LoadingService);

  /** The loaded restaurant's details async-state (loading / error / loaded). */
  private readonly detailsState = this.loadingService.create<RestaurantDetailsDto>();
  /** The save (`PUT /admin/menu-items/{id}`) async-state — 204 success / typed failure. */
  private readonly saveState = this.loadingService.create<void>();

  /** The id of the last restaurant the operator loaded (drives reload). */
  private readonly restaurantId = signal<string | null>(null);
  /** The menu item currently selected for editing, or `null`. */
  private readonly selected = signal<MenuItemDto | null>(null);
  /** The category id currently chosen in the edit form (drives the per-category dish load). */
  private readonly editorCategoryId = signal<string | null>(null);

  // ── Bound read async-state ────────────────────────────────────────────────
  readonly loading = this.detailsState.loading;
  readonly error = this.detailsState.error;
  readonly restaurant = this.detailsState.data;

  // ── Bound write async-state ───────────────────────────────────────────────
  readonly saving = this.saveState.loading;
  readonly saved = this.saveState.loaded;
  readonly saveError = this.saveState.error;

  /** The full taxonomy top level for the re-categorisation picker. */
  readonly categories = computed(() => this.catalog.categories() ?? []);
  /** The id of the selected menu item (highlights the active row). */
  readonly selectedId = computed(() => this.selected()?.id ?? null);

  constructor() {
    // The taxonomy is cached in the store — load it once for the re-categorisation picker.
    this.catalog.loadCategories();

    // When a new item is selected, point the editor at its category and ensure that category's
    // dishes are loaded so the dish `<mat-select>` is populated for the re-categorisation picker.
    effect(() => {
      const seed = this.seed();
      if (seed !== null && seed.categoryId.length > 0) {
        this.editorCategoryId.set(seed.categoryId);
        this.catalog.loadDishes(seed.categoryId);
      }
    });
  }

  /** The edit-form seed for the selected item (resolving its dish's category from the taxonomy). */
  readonly seed = computed<MenuEditSeed | null>(() => {
    const item = this.selected();
    if (item === null) {
      return null;
    }
    const categoryId = this.categoryOfDish(item.dishId);
    return {
      categoryId,
      dishId: item.dishId,
      priceAmount: item.priceAmount,
      priceCurrency: item.priceCurrency,
      weight: item.weight ?? null,
    };
  });

  /** The dishes for the category currently chosen in the editor (for the dish `<mat-select>`). */
  readonly editorDishes = computed<readonly DishDto[]>(() => {
    const categoryId = this.editorCategoryId();
    return categoryId === null ? [] : (this.catalog.dishes(categoryId)() ?? []);
  });

  /** True while the editor's category dishes are loading. */
  readonly editorDishesLoading = computed<boolean>(() => {
    const categoryId = this.editorCategoryId();
    return categoryId === null ? false : this.catalog.dishesLoading(categoryId)();
  });

  /** Fetch the restaurant's details (its menu items). Clears any prior selection/save state. */
  loadRestaurant(id: string): void {
    this.restaurantId.set(id);
    this.selected.set(null);
    this.saveState.reset();
    this.detailsState.run(this.getDetails.execute(id));
  }

  /** Re-run the details fetch (the shared error-state retry affordance). */
  reload(): void {
    const id = this.restaurantId();
    if (id !== null) {
      this.detailsState.run(this.getDetails.execute(id));
    }
  }

  /** Select a menu item for editing (the anchor is a list affordance — prevent its default nav). */
  selectItem(item: MenuItemDto, event: Event): void {
    event.preventDefault();
    this.saveState.reset();
    this.selected.set(item);
  }

  /** The edit form asked for a different category's dishes (taxonomy narrowing). */
  loadCategoryDishes(categoryId: string): void {
    this.editorCategoryId.set(categoryId);
    this.catalog.loadDishes(categoryId);
  }

  /** Perform the menu-item edit write (`PUT /admin/menu-items/{id}`); 204 → saved, else typed error. */
  save(body: EditMenuItemBody): void {
    const item = this.selected();
    if (item === null) {
      return;
    }
    this.saveState.run(this.editMenuItem.execute(item.id, body));
  }

  /** The dish's display name from the cached taxonomy, or a neutral fallback (no extra fetch). */
  dishName(dishId: string): string {
    const name = this.catalog.dishNameById(dishId)();
    return name ?? dishId;
  }

  /** The category id that owns a dish, scanning the cached taxonomy (empty when not yet loaded). */
  private categoryOfDish(dishId: string): string {
    for (const category of this.categories()) {
      const dishes = this.catalog.dishes(category.id)();
      if (dishes?.some((d) => d.id === dishId)) {
        return category.id;
      }
    }
    return '';
  }
}
