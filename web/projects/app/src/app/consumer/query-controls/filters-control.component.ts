import { ChangeDetectionStrategy, Component, computed, inject, input, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import type { FilterKey, FilterSelection } from 'core';

/**
 * Renderer kind for a {@link FilterDescriptor}. The template dispatches on this discriminant, so a new
 * non-numeric filter (open-now toggle, vegan, delivery, …) is added by widening this union and adding a
 * matching `@if (descriptor.kind === '…')` branch + renderer block in the template — existing kinds and
 * filters stay untouched (invariant #4 — pluggable filters). `'numeric'` is the only kind needed today
 * (price max / rating min); the union and its template dispatch exist so the seam is genuinely additive.
 */
export type FilterKind = 'numeric';

/**
 * Declarative descriptor for one composable filter. The control renders a row per descriptor, so a new
 * filter is added by appending a descriptor — **no template or logic rewrite for an existing kind**
 * (invariant #4 — pluggable filters). The {@link FilterKind} discriminant selects the renderer.
 */
export interface FilterDescriptor {
  readonly key: FilterKey;
  /** i18n catalog key for the field label (resolved in the template) — no launch-locale string here. */
  readonly labelKey: string;
  readonly icon: string;
  /** Renderer kind; the template dispatches on it. Widen {@link FilterKind} + add a branch for new kinds. */
  readonly kind: FilterKind;
  /** i18n catalog key for the caption clarifying the bound (e.g. "max price", "min rating"). */
  readonly hintKey?: string;
  /** Optional input attributes for a numeric filter. */
  readonly min?: number;
  readonly max?: number;
  readonly step?: number;
}

/** The default filter descriptors: `price` (max) and `rating` (min). Pinned to the contract keys. */
export const DEFAULT_FILTER_DESCRIPTORS: readonly FilterDescriptor[] = [
  {
    key: 'price',
    labelKey: 'consumer.filters.maxPrice',
    icon: 'payments',
    kind: 'numeric',
    hintKey: 'consumer.filters.maxPriceHint',
    min: 0,
    step: 1,
  },
  {
    key: 'rating',
    labelKey: 'consumer.filters.minRating',
    icon: 'star',
    kind: 'numeric',
    hintKey: 'consumer.filters.minRatingHint',
    min: 0,
    max: 5,
    step: 0.5,
  },
];

/**
 * Composable filters control (dumb / presentational).
 *
 * Renders one row per {@link FilterDescriptor}, fully **data-driven** so the filter set is additive
 * (invariant #4): adding a descriptor renders a new filter with no code change here. Today it carries
 * `price` (max) and `rating` (min). It owns no state — the container binds the applied
 * {@link FilterSelection}s and the component emits `apply` (key + value) / `clear` (key); pure
 * inputs/outputs so it mirrors 1:1 onto an RN form.
 *
 * Each numeric field is a labelled Material input (WCAG): a non-empty value emits `apply`, clearing it
 * emits `clear`, so the container can drop the filter from the selection entirely rather than sending
 * an empty value.
 */
@Component({
  selector: 'app-filters-control',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatFormFieldModule, MatInputModule, MatButtonModule, MatIconModule, TranslatePipe],
  template: `
    <fieldset class="dp-filters" data-testid="filters-control">
      <legend class="dp-filters__legend">{{ 'consumer.filters.legend' | translate }}</legend>

      <div class="dp-filters__rows">
        @for (descriptor of descriptors(); track descriptor.key) {
          <div class="dp-filters__row" [attr.data-testid]="'filter-' + descriptor.key">
            <!-- Renderer dispatch on the {@link FilterKind} discriminant. A future non-numeric kind
                 adds its own @if branch + block here; the numeric block below stays untouched. -->
            @if (descriptor.kind === 'numeric') {
              <mat-form-field appearance="outline" class="dp-filters__field">
                <mat-label>{{ descriptor.labelKey | translate }}</mat-label>
                <mat-icon matIconPrefix aria-hidden="true">{{ descriptor.icon }}</mat-icon>
                <input
                  matInput
                  type="number"
                  inputmode="decimal"
                  [attr.data-testid]="'filter-input-' + descriptor.key"
                  [value]="valueFor(descriptor.key)"
                  [min]="descriptor.min ?? null"
                  [max]="descriptor.max ?? null"
                  [step]="descriptor.step ?? null"
                  (input)="onInput(descriptor.key, $event)"
                />
                @if (descriptor.hintKey) {
                  <mat-hint>{{ descriptor.hintKey | translate }}</mat-hint>
                }
              </mat-form-field>
            }

            @if (isApplied(descriptor.key)) {
              <button
                mat-icon-button
                type="button"
                class="dp-filters__clear"
                [attr.data-testid]="'filter-clear-' + descriptor.key"
                [attr.aria-label]="clearAria(descriptor.labelKey)"
                (click)="clear.emit(descriptor.key)"
              >
                <mat-icon aria-hidden="true">close</mat-icon>
              </button>
            }
          </div>
        }
      </div>
    </fieldset>
  `,
  styles: `
    .dp-filters {
      display: flex;
      flex-direction: column;
      gap: var(--dp-space-2);
      margin: 0;
      padding: 0;
      border: none;
    }

    .dp-filters__legend {
      padding: 0;
      font-size: var(--dp-font-size-subtitle);
      font-weight: var(--dp-font-weight-bold);
    }

    .dp-filters__rows {
      display: flex;
      flex-direction: column;
      gap: var(--dp-space-2);
    }

    .dp-filters__row {
      display: flex;
      align-items: flex-start;
      gap: var(--dp-space-2);
    }

    .dp-filters__field {
      flex: 1 1 auto;
    }
  `,
})
export class FiltersControlComponent {
  private readonly translate = inject(TranslateService);

  /** The filters currently applied (one per key), bound from the container. */
  readonly applied = input<readonly FilterSelection[]>([]);

  /**
   * The filter descriptors to render, in order. Defaults to {@link DEFAULT_FILTER_DESCRIPTORS}
   * (price max + rating min); a new filter is added by supplying a longer list — no code change.
   */
  readonly descriptors = input<readonly FilterDescriptor[]>(DEFAULT_FILTER_DESCRIPTORS);

  /** Emitted when a filter value is set (a non-empty input) — key + the raw value string. */
  readonly apply = output<{ readonly key: FilterKey; readonly value: string }>();
  /** Emitted when a filter is cleared (its input emptied or its clear button pressed). */
  readonly clear = output<FilterKey>();

  /** Index of the applied filters by key so per-key lookups in the template are O(1). */
  private readonly byKey = computed<ReadonlyMap<FilterKey, FilterSelection>>(
    () => new Map(this.applied().map((f) => [f.key, f])),
  );

  /** The current value of a filter for the input's `[value]`, or empty when not applied. */
  valueFor(key: FilterKey): string {
    return this.byKey().get(key)?.value ?? '';
  }

  /** True when a filter is currently applied (drives the per-row clear affordance). */
  isApplied(key: FilterKey): boolean {
    return this.byKey().has(key);
  }

  /** A non-empty input applies the filter; emptying it clears the filter entirely (no empty value). */
  onInput(key: FilterKey, event: Event): void {
    const raw = (event.target as HTMLInputElement).value.trim();
    if (raw.length === 0) {
      this.clear.emit(key);
      return;
    }
    this.apply.emit({ key, value: raw });
  }

  /**
   * The accessible label for a filter's clear button (WCAG 4.1.2): resolves the filter's `labelKey`,
   * then interpolates it into the `clearAria` catalog string. Synchronous `instant` is correct here —
   * the catalogs are bundled in-memory (Step 17), so resolution does not need an async round-trip.
   */
  clearAria(labelKey: string): string {
    const label = this.translate.instant(labelKey);
    return this.translate.instant('consumer.filters.clearAria', { label });
  }
}
