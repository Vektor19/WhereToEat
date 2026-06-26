import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  output,
} from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators, type FormGroup } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { TranslatePipe } from '@ngx-translate/core';
import type { CategoryDto, DishDto, EditMenuItemBody } from 'core';

/** The form's initial values, derived by the container from the selected menu item. */
export interface MenuEditSeed {
  readonly categoryId: string;
  readonly dishId: string;
  readonly priceAmount: number;
  readonly priceCurrency: string;
  readonly weight: string | null;
}

/**
 * Menu-item edit form (dumb / presentational) — Step 19 (menu management, admin > parser invariant #3).
 *
 * Edits a single menu item's price/weight and **re-categorises** it by changing the dish through the
 * two-level Category → Dish taxonomy picker (invariant #2): choosing a category narrows the dish
 * `<mat-select>` to that category's dishes (the container supplies the dishes for the chosen category).
 * On submit it emits the exact {@link EditMenuItemBody} the `PUT /admin/menu-items/{id}` endpoint
 * expects (`dishId`, `priceAmount`, `priceCurrency`, optional `weight`) — the container owns the write.
 *
 * It owns the typed reactive form only (no data access), so it mirrors 1:1 onto an RN form. Fields are
 * accessible Material controls (label + ARIA + keyboard for free); price/currency are required and
 * validated client-side so the UI fails fast before the round-trip. Copy resolves from the runtime
 * i18n catalogs; themed from design tokens.
 */
@Component({
  selector: 'app-menu-edit-form',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    TranslatePipe,
  ],
  template: `
    <form
      class="dp-menu-edit"
      data-testid="menu-edit-form"
      [formGroup]="form"
      (ngSubmit)="onSubmit()"
    >
      <h2 class="dp-menu-edit__title">{{ 'portal.menu.editTitle' | translate }}</h2>

      <!-- Two-level taxonomy re-categorisation: category narrows the dish list (invariant #2). -->
      <mat-form-field appearance="outline">
        <mat-label>{{ 'portal.menu.category' | translate }}</mat-label>
        <mat-select formControlName="categoryId" data-testid="menu-category-select">
          @for (category of categories(); track category.id) {
            <mat-option [value]="category.id">{{ category.name }}</mat-option>
          }
        </mat-select>
      </mat-form-field>

      <mat-form-field appearance="outline">
        <mat-label>{{ 'portal.menu.dish' | translate }}</mat-label>
        <mat-select formControlName="dishId" data-testid="menu-dish-select">
          @for (dish of dishes(); track dish.id) {
            <mat-option [value]="dish.id">{{ dish.canonicalName }}</mat-option>
          }
        </mat-select>
        @if (dishesLoading()) {
          <mat-hint data-testid="menu-dish-loading">{{
            'portal.menu.dishesLoading' | translate
          }}</mat-hint>
        }
      </mat-form-field>

      <div class="dp-menu-edit__price">
        <mat-form-field appearance="outline" class="dp-menu-edit__amount">
          <mat-label>{{ 'portal.menu.price' | translate }}</mat-label>
          <input
            matInput
            type="number"
            inputmode="decimal"
            min="0"
            step="0.01"
            formControlName="priceAmount"
            data-testid="menu-price-input"
          />
        </mat-form-field>

        <mat-form-field appearance="outline" class="dp-menu-edit__currency">
          <mat-label>{{ 'portal.menu.currency' | translate }}</mat-label>
          <input
            matInput
            type="text"
            maxlength="3"
            autocomplete="off"
            formControlName="priceCurrency"
            data-testid="menu-currency-input"
          />
        </mat-form-field>
      </div>

      <mat-form-field appearance="outline">
        <mat-label>{{ 'portal.menu.weight' | translate }}</mat-label>
        <input
          matInput
          type="text"
          autocomplete="off"
          formControlName="weight"
          data-testid="menu-weight-input"
        />
        <mat-hint>{{ 'portal.menu.weightHint' | translate }}</mat-hint>
      </mat-form-field>

      <button
        mat-flat-button
        color="primary"
        type="submit"
        class="dp-menu-edit__save"
        data-testid="menu-save"
        [disabled]="form.invalid || saving()"
      >
        <mat-icon aria-hidden="true">save</mat-icon>
        {{ 'portal.menu.save' | translate }}
      </button>
    </form>
  `,
  styles: `
    :host {
      display: block;
    }

    .dp-menu-edit {
      display: flex;
      flex-direction: column;
      gap: var(--dp-space-3);
      max-width: var(--dp-layout-content-max);
    }

    .dp-menu-edit__title {
      margin: 0;
      font-size: var(--dp-font-size-subtitle);
      font-weight: var(--dp-font-weight-bold);
    }

    .dp-menu-edit__price {
      display: flex;
      gap: var(--dp-space-3);
      flex-wrap: wrap;
    }

    .dp-menu-edit__amount {
      flex: 2 1 var(--dp-field-basis-sm);
    }

    .dp-menu-edit__currency {
      flex: 1 1 var(--dp-field-basis-xs);
    }

    .dp-menu-edit__save {
      align-self: flex-start;
    }
  `,
})
export class MenuEditFormComponent {
  private readonly fb = inject(FormBuilder);

  /** The full category list (taxonomy top level) for the re-categorisation picker. */
  readonly categories = input.required<readonly CategoryDto[]>();
  /** The dishes within the **currently-selected** category (the container loads them per category). */
  readonly dishes = input.required<readonly DishDto[]>();
  /** True while the selected category's dishes are loading (shows a discreet hint). */
  readonly dishesLoading = input<boolean>(false);
  /** The initial form values for the item being edited. */
  readonly seed = input.required<MenuEditSeed>();
  /** True while the save write is in flight (disables submit). */
  readonly saving = input<boolean>(false);

  /** Emitted with the exact `PUT /admin/menu-items/{id}` body on a valid submit. */
  readonly save = output<EditMenuItemBody>();
  /** Emitted when the operator picks a different category (the container loads its dishes). */
  readonly categoryChange = output<string>();

  /** The typed reactive form — price/currency required, dish required (re-categorisation target). */
  readonly form: FormGroup = this.fb.group({
    categoryId: ['', Validators.required],
    dishId: ['', Validators.required],
    priceAmount: [0, [Validators.required, Validators.min(0)]],
    priceCurrency: ['', [Validators.required, Validators.maxLength(3)]],
    weight: [''],
  });

  /** The currently-chosen category id (so the container knows which dishes to load). */
  readonly selectedCategoryId = computed(() => this.seed().categoryId);

  constructor() {
    // Re-seed the form whenever the container hands a new item to edit.
    effect(() => {
      const seed = this.seed();
      this.form.reset({
        categoryId: seed.categoryId,
        dishId: seed.dishId,
        priceAmount: seed.priceAmount,
        priceCurrency: seed.priceCurrency,
        weight: seed.weight ?? '',
      });
    });

    // A category change re-asks the container for that category's dishes (taxonomy narrowing).
    this.form.controls['categoryId'].valueChanges.subscribe((categoryId: string | null) => {
      if (categoryId) {
        this.categoryChange.emit(categoryId);
      }
    });
  }

  /** Build the exact endpoint body and emit it (the container performs the write). */
  onSubmit(): void {
    if (this.form.invalid) {
      return;
    }
    const raw = this.form.getRawValue() as {
      dishId: string;
      priceAmount: number;
      priceCurrency: string;
      weight: string;
    };
    const weight = raw.weight.trim();
    this.save.emit({
      dishId: raw.dishId,
      priceAmount: Number(raw.priceAmount),
      priceCurrency: raw.priceCurrency.trim().toUpperCase(),
      weight: weight.length > 0 ? weight : null,
    });
  }
}
