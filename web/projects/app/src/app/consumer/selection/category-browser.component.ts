import { ChangeDetectionStrategy, Component, computed, inject, input, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import type { ApiError, CategoryDto, DishDto } from 'core';

import { ErrorStateComponent, LoadingComponent } from '../../shared/ui';

/**
 * Two-level taxonomy browser (dumb / presentational — invariant #2).
 *
 * Renders the categories list and, once a category is opened, that category's dishes — the user can
 * **add a whole category** or **drill in and add a specific dish**. It owns no data: the container
 * feeds the cached categories/dishes (from the {@link CatalogStore}) and the open category, and the
 * component emits `openCategory` (drill in), `closeCategory` (back up), `addCategory`, and `addDish`.
 * Pure inputs/outputs so it mirrors 1:1 onto an RN list view.
 *
 * Loading/empty/error states reuse the shared primitives so they look the same everywhere (DRY).
 * Each row exposes a keyboard-reachable button and a screen-reader label (WCAG).
 */
@Component({
  selector: 'app-category-browser',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    MatButtonModule,
    MatIconModule,
    MatListModule,
    LoadingComponent,
    ErrorStateComponent,
    TranslatePipe,
  ],
  template: `
    @if (openCategory(); as open) {
      <!-- Drilled into a category: show its dishes (the lower taxonomy level). -->
      <div class="dp-browse">
        <div class="dp-browse__bar">
          <button mat-button type="button" (click)="closeCategory.emit()" data-testid="browse-back">
            <mat-icon aria-hidden="true">arrow_back</mat-icon>
            {{ 'consumer.browse.categories' | translate }}
          </button>
          <span class="dp-browse__title">{{ open.name }}</span>
        </div>

        <button
          mat-stroked-button
          type="button"
          class="dp-browse__add-category"
          [disabled]="isCategorySelected()"
          (click)="addCategory.emit(open)"
          data-testid="browse-add-open-category"
        >
          <mat-icon aria-hidden="true">{{ isCategorySelected() ? 'check' : 'add' }}</mat-icon>
          {{
            (isCategorySelected()
              ? 'consumer.browse.categoryAdded'
              : 'consumer.browse.addWholeCategory'
            ) | translate
          }}
        </button>

        @if (dishesLoading()) {
          <app-loading [label]="'consumer.browse.dishesLoading' | translate" />
        } @else if (dishesError()) {
          <app-error-state [error]="dishesError()" (retry)="reloadDishes.emit(open)" />
        } @else if (dishesEmpty()) {
          <app-error-state [emptyMessage]="'consumer.browse.dishesEmpty' | translate" />
        } @else {
          <ul
            class="dp-browse__list"
            [attr.aria-label]="'consumer.browse.dishesOfCategory' | translate"
            data-testid="dish-list"
          >
            @for (dish of dishes(); track dish.id) {
              <li class="dp-browse__row">
                <button
                  mat-button
                  type="button"
                  class="dp-browse__row-btn"
                  [disabled]="isDishSelected(dish.id)"
                  (click)="addDish.emit(dish)"
                  [attr.aria-label]="addDishAria(dish.canonicalName)"
                  [attr.data-testid]="'dish-' + dish.id"
                >
                  <mat-icon aria-hidden="true">{{
                    isDishSelected(dish.id) ? 'check' : 'add'
                  }}</mat-icon>
                  <span class="dp-browse__row-label">{{ dish.canonicalName }}</span>
                </button>
              </li>
            }
          </ul>
        }
      </div>
    } @else {
      <!-- Top level: the categories (groups of dishes). -->
      <div class="dp-browse">
        @if (categoriesLoading()) {
          <app-loading [label]="'consumer.browse.categoriesLoading' | translate" />
        } @else if (categoriesError()) {
          <app-error-state [error]="categoriesError()" (retry)="reloadCategories.emit()" />
        } @else if (categoriesEmpty()) {
          <app-error-state [emptyMessage]="'consumer.browse.categoriesEmpty' | translate" />
        } @else {
          <ul
            class="dp-browse__list"
            [attr.aria-label]="'consumer.browse.categories' | translate"
            data-testid="category-list"
          >
            @for (category of categories(); track category.id) {
              <li class="dp-browse__row dp-browse__row--split">
                <button
                  mat-button
                  type="button"
                  class="dp-browse__row-btn"
                  (click)="openCategoryRequest.emit(category)"
                  [attr.aria-label]="openCategoryAria(category.name)"
                  [attr.data-testid]="'category-' + category.id"
                >
                  <mat-icon aria-hidden="true">category</mat-icon>
                  <span class="dp-browse__row-label">{{ category.name }}</span>
                  <mat-icon aria-hidden="true">chevron_right</mat-icon>
                </button>
                <button
                  mat-icon-button
                  type="button"
                  [disabled]="isCategoryIdSelected(category.id)"
                  (click)="addCategory.emit(category)"
                  [attr.aria-label]="addCategoryAria(category.name)"
                  [attr.data-testid]="'add-category-' + category.id"
                >
                  <mat-icon aria-hidden="true">{{
                    isCategoryIdSelected(category.id) ? 'check' : 'add'
                  }}</mat-icon>
                </button>
              </li>
            }
          </ul>
        }
      </div>
    }
  `,
  styles: `
    .dp-browse {
      display: flex;
      flex-direction: column;
      gap: var(--dp-space-3);
    }

    .dp-browse__bar {
      display: flex;
      align-items: center;
      gap: var(--dp-space-3);
    }

    .dp-browse__title {
      font-weight: var(--dp-font-weight-bold);
      font-size: var(--dp-font-size-subtitle);
    }

    .dp-browse__add-category {
      align-self: flex-start;
    }

    .dp-browse__list {
      list-style: none;
      margin: 0;
      padding: 0;
      border: var(--dp-border-width-hairline) solid var(--dp-color-outline-variant);
      border-radius: var(--dp-radius-md);
      overflow: hidden;
    }

    .dp-browse__row {
      display: flex;
      align-items: center;
    }

    .dp-browse__row + .dp-browse__row {
      border-top: var(--dp-border-width-hairline) solid var(--dp-color-outline-variant);
    }

    .dp-browse__row--split {
      justify-content: space-between;
    }

    .dp-browse__row-btn {
      flex: 1 1 auto;
      justify-content: flex-start;
      gap: var(--dp-space-3);
    }

    .dp-browse__row-label {
      flex: 1 1 auto;
      text-align: start;
    }
  `,
})
export class CategoryBrowserComponent {
  private readonly translate = inject(TranslateService);

  // ── Categories (top level) ──────────────────────────────────────────────────
  readonly categories = input<readonly CategoryDto[]>([]);
  readonly categoriesLoading = input<boolean>(false);
  readonly categoriesError = input<ApiError | undefined>(undefined);
  readonly categoriesEmpty = input<boolean>(false);

  // ── The opened category and its dishes (lower level) ────────────────────────
  /** The category the user drilled into, or `undefined` at the top level. */
  readonly openCategory = input<CategoryDto | undefined>(undefined);
  readonly dishes = input<readonly DishDto[]>([]);
  readonly dishesLoading = input<boolean>(false);
  readonly dishesError = input<ApiError | undefined>(undefined);
  readonly dishesEmpty = input<boolean>(false);

  /** The currently-selected category/dish ids, so already-added rows are shown as added. */
  readonly selectedCategoryIds = input<readonly string[]>([]);
  readonly selectedDishIds = input<readonly string[]>([]);

  // ── Outputs ─────────────────────────────────────────────────────────────────
  readonly openCategoryRequest = output<CategoryDto>();
  readonly closeCategory = output<void>();
  readonly addCategory = output<CategoryDto>();
  readonly addDish = output<DishDto>();
  readonly reloadCategories = output<void>();
  readonly reloadDishes = output<CategoryDto>();

  /** True when the opened category is already in the selection. */
  readonly isCategorySelected = computed<boolean>(() => {
    const open = this.openCategory();
    return open !== undefined && this.selectedCategoryIds().includes(open.id);
  });

  isCategoryIdSelected(categoryId: string): boolean {
    return this.selectedCategoryIds().includes(categoryId);
  }

  isDishSelected(dishId: string): boolean {
    return this.selectedDishIds().includes(dishId);
  }

  openCategoryAria(name: string): string {
    return this.translate.instant('consumer.browse.openCategoryAria', { name });
  }

  addCategoryAria(name: string): string {
    return this.translate.instant('consumer.browse.addCategoryAria', { name });
  }

  addDishAria(name: string): string {
    return this.translate.instant('consumer.browse.addDishAria', { name });
  }
}
