import { ChangeDetectionStrategy, Component, computed, inject, input, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatIconModule } from '@angular/material/icon';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import type { CategorySelection, DishSelection, SelectedItem } from 'core';

/**
 * One selected item as the chips list renders it — the underlying {@link SelectedItem} plus the
 * resolved display label (the container resolves the id to a category/dish name from the catalog).
 *
 * Modelled as a discriminated union on `kind` so the chip's `item` is narrowed to the matching
 * selection variant: a `category` chip carries a {@link CategorySelection} (so `categoryId` is
 * always present) and a `dish` chip a {@link DishSelection} (so `dishId` is always present). This
 * makes {@link SelectionSummaryComponent.chipKey} provably correct — no optional-id fallback.
 */
export type SelectionChip =
  | { readonly kind: 'category'; readonly item: CategorySelection; readonly label: string }
  | { readonly kind: 'dish'; readonly item: DishSelection; readonly label: string };

/**
 * Current-selection chips (dumb / presentational).
 *
 * Renders the selection list as removable chips with a "clear all" affordance. It owns no state:
 * the container maps the {@link SelectionStore}'s items to {@link SelectionChip}s (resolving labels
 * from the catalog) and the component emits `remove` / `clear`. Pure inputs/outputs so it mirrors
 * onto an RN chip row. Empty state renders a quiet hint rather than an error panel — an empty
 * selection is a normal starting state, not a failure.
 *
 * Each chip's remove button carries a screen-reader label; the region is a labeled landmark (WCAG).
 */
@Component({
  selector: 'app-selection-summary',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule, MatChipsModule, MatIconModule, TranslatePipe],
  template: `
    <section
      class="dp-selection"
      [attr.aria-label]="'consumer.selection.regionLabel' | translate"
      data-testid="selection-summary"
    >
      <div class="dp-selection__header">
        <h2 class="dp-selection__title">{{ 'consumer.selection.title' | translate }}</h2>
        @if (hasItems()) {
          <button
            mat-button
            type="button"
            (click)="clear.emit()"
            data-testid="selection-clear"
            [attr.aria-label]="'consumer.selection.clearAll' | translate"
          >
            <mat-icon aria-hidden="true">clear_all</mat-icon>
            {{ 'consumer.selection.clearAll' | translate }}
          </button>
        }
      </div>

      @if (hasItems()) {
        <mat-chip-set [attr.aria-label]="'consumer.selection.regionLabel' | translate">
          @for (chip of chips(); track chipKey(chip)) {
            <mat-chip class="dp-selection__chip" [attr.data-testid]="'selection-chip-' + chip.kind">
              <mat-icon matChipAvatar aria-hidden="true">{{
                chip.kind === 'category' ? 'category' : 'restaurant_menu'
              }}</mat-icon>
              {{ chip.label }}
              <button
                matChipRemove
                type="button"
                (click)="remove.emit(chip.item)"
                [attr.aria-label]="removeAria(chip.label)"
                [attr.data-testid]="'selection-remove-' + chipKey(chip)"
              >
                <mat-icon aria-hidden="true">cancel</mat-icon>
              </button>
            </mat-chip>
          }
        </mat-chip-set>
      } @else {
        <p class="dp-selection__empty" data-testid="selection-empty">
          {{ 'consumer.selection.emptyHint' | translate }}
        </p>
      }
    </section>
  `,
  styles: `
    .dp-selection {
      display: flex;
      flex-direction: column;
      gap: var(--dp-space-3);
    }

    .dp-selection__header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: var(--dp-space-3);
    }

    .dp-selection__title {
      margin: 0;
      font-size: var(--dp-font-size-subtitle);
      font-weight: var(--dp-font-weight-bold);
    }

    .dp-selection__empty {
      margin: 0;
      color: var(--dp-color-on-surface-variant);
      font-size: var(--dp-font-size-body);
    }
  `,
})
export class SelectionSummaryComponent {
  private readonly translate = inject(TranslateService);

  /** The resolved selection chips fed by the container. */
  readonly chips = input<readonly SelectionChip[]>([]);

  // ── Outputs ─────────────────────────────────────────────────────────────────
  readonly remove = output<SelectedItem>();
  readonly clear = output<void>();

  readonly hasItems = computed<boolean>(() => this.chips().length > 0);

  /** A stable per-chip key for `track` and the remove test id (kind + resolved id). */
  chipKey(chip: SelectionChip): string {
    const id = chip.kind === 'category' ? chip.item.categoryId : chip.item.dishId;
    return `${chip.kind}-${id}`;
  }

  /** The accessible remove-chip label (WCAG): the `removeAria` catalog string with the chip label. */
  removeAria(label: string): string {
    return this.translate.instant('consumer.selection.removeAria', { label });
  }
}
