import { ChangeDetectionStrategy, Component, computed, inject, input, output } from '@angular/core';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatIconModule } from '@angular/material/icon';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { translateText, type SortMode } from 'core';

/**
 * One selectable sort option the control renders, in display order. `explicit` marks the three
 * single-field sorts (`price` / `distance` / `rating`) where the chosen field dominates the order and
 * coverage is only a tie-breaker (invariant #5); the two composite modes (`price-quality` / `best`)
 * are `explicit: false` because they rank by a composed score, not one field. The list is data-driven
 * so a future composite/explicit mode is additive (invariant #4 — pluggable engine keys). `labelKey`
 * is the i18n catalog key for the button label (resolved in the template / hint), so the option list
 * carries no launch-locale string.
 */
interface SortOption {
  readonly mode: SortMode;
  readonly labelKey: string;
  readonly icon: string;
  readonly explicit: boolean;
}

/**
 * Sort-mode control (dumb / presentational).
 *
 * Offers exactly the five pinned contract sort values — the three **explicit single-field** sorts
 * (`price` / `distance` / `rating`) and the two **composite** sorts (`price-quality` / `best`). It
 * owns no state: the container binds the current {@link SortMode} and the component emits the chosen
 * one; pure inputs/outputs so it mirrors 1:1 onto an RN segmented control.
 *
 * **Explicit-sort-dominates UX (invariant #5).** This control carries the dominance rule in its copy:
 * when an explicit single-field sort is active, a helper line states that results are ordered by that
 * field and coverage ("N of M") is only a tie-breaker — so the UI never implies coverage is the
 * primary ordering. When a composite sort is active the helper line instead explains the composed
 * balance. The result-card coverage rendering itself is Step 12; this control communicates the rule.
 *
 * Built on Material's button-toggle group for an accessible single-select group (roving focus,
 * arrow-key navigation, `aria-pressed`) for free (WCAG).
 */
@Component({
  selector: 'app-sort-control',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonToggleModule, MatIconModule, TranslatePipe],
  template: `
    <fieldset class="dp-sort" data-testid="sort-control">
      <legend class="dp-sort__legend">{{ 'consumer.sort.legend' | translate }}</legend>

      <mat-button-toggle-group
        class="dp-sort__group"
        [value]="value()"
        (change)="modeChange.emit($event.value)"
        [attr.aria-label]="'consumer.sort.legend' | translate"
      >
        @for (option of options(); track option.mode) {
          <mat-button-toggle [value]="option.mode" [attr.data-testid]="'sort-' + option.mode">
            <mat-icon aria-hidden="true">{{ option.icon }}</mat-icon>
            {{ option.labelKey | translate }}
          </mat-button-toggle>
        }
      </mat-button-toggle-group>

      <p
        class="dp-sort__hint"
        [class.dp-sort__hint--explicit]="isExplicit()"
        data-testid="sort-hint"
        [attr.data-explicit]="isExplicit()"
      >
        <mat-icon class="dp-sort__hint-icon" aria-hidden="true">info</mat-icon>
        {{ hint() }}
      </p>
    </fieldset>
  `,
  styles: `
    .dp-sort {
      display: flex;
      flex-direction: column;
      gap: var(--dp-space-2);
      margin: 0;
      padding: 0;
      border: none;
    }

    .dp-sort__legend {
      padding: 0;
      font-size: var(--dp-font-size-subtitle);
      font-weight: var(--dp-font-weight-bold);
    }

    .dp-sort__group {
      flex-wrap: wrap;
    }

    .dp-sort__hint {
      display: flex;
      align-items: flex-start;
      gap: var(--dp-space-2);
      margin: 0;
      color: var(--dp-color-on-surface-variant);
      font-size: var(--dp-font-size-caption);
    }

    .dp-sort__hint-icon {
      flex: none;
      font-size: var(--dp-font-size-body);
      width: var(--dp-font-size-body);
      height: var(--dp-font-size-body);
    }
  `,
})
export class SortControlComponent {
  private readonly translate = inject(TranslateService);

  /** The currently selected sort mode. */
  readonly value = input.required<SortMode>();

  /**
   * The selectable sort options in display order. Data-driven (invariant #4): the three explicit
   * single-field sorts first, then the two composite modes. The `explicit` flag drives the
   * dominance copy; a new mode is a new entry (with its `labelKey`), no control rewrite.
   */
  readonly options = input<readonly SortOption[]>([
    { mode: 'price', labelKey: 'consumer.sort.price', icon: 'payments', explicit: true },
    { mode: 'distance', labelKey: 'consumer.sort.distance', icon: 'near_me', explicit: true },
    { mode: 'rating', labelKey: 'consumer.sort.rating', icon: 'star', explicit: true },
    {
      mode: 'price-quality',
      labelKey: 'consumer.sort.priceQuality',
      icon: 'balance',
      explicit: false,
    },
    { mode: 'best', labelKey: 'consumer.sort.best', icon: 'auto_awesome', explicit: false },
  ]);

  /** Emitted with the newly chosen sort mode. */
  readonly modeChange = output<SortMode>();

  /** True when the active mode is an explicit single-field sort (so the dominance copy applies). */
  readonly isExplicit = computed<boolean>(
    () => this.options().find((o) => o.mode === this.value())?.explicit ?? false,
  );

  /** The active option's label key, for interpolating the field name into the dominance hint. */
  private readonly activeLabelKey = computed<string>(
    () => this.options().find((o) => o.mode === this.value())?.labelKey ?? '',
  );

  /**
   * The dominance / balance helper line (invariant #5). For an explicit single-field sort it states
   * the chosen field orders the list and coverage is only a tie-breaker (the field name is the
   * resolved active label); for a composite mode it explains the composed balance instead. The
   * reactive `translate()` signal tracks `value()`/`options()` and re-resolves on a language switch.
   */
  readonly hint = translateText(
    this.translate,
    () => (this.isExplicit() ? 'consumer.sort.hintExplicit' : 'consumer.sort.hintComposite'),
    () =>
      this.isExplicit() ? { field: this.translate.instant(this.activeLabelKey()) as string } : {},
  );
}
