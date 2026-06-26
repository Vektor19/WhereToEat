import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  input,
  output,
  signal,
} from '@angular/core';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { TranslatePipe } from '@ngx-translate/core';
import type { ApiError } from 'core';

import { ErrorStateComponent, LoadingComponent } from '../../shared/ui';

/**
 * One row in the prefix-search pick-list — a category or a dish the user can add to the selection.
 * `kind` keeps the two taxonomy levels distinct (invariant #2) so the container adds exactly the
 * right `SelectedItem` (a `categoryId` or a `dishId`, never both).
 */
export interface PrefixSearchResult {
  readonly id: string;
  readonly label: string;
  readonly kind: 'category' | 'dish';
  /** True when the item is already in the selection (so the row is shown as added, not re-addable). */
  readonly selected: boolean;
}

/**
 * Deterministic **anchored prefix-search typeahead** (dumb / presentational — invariant #1).
 *
 * The input only filters the category/dish lists by prefix; **no free text reaches the recommend
 * engine** — the container debounces the `query` output and runs the prefix-search data-access
 * ports, feeding the matches back in as `results`. Picking a row emits `pick` so the container adds
 * the right `SelectedItem`. This component owns no data and no debounce; it is pure inputs/outputs so
 * it mirrors 1:1 onto an RN control.
 *
 * Accessibility: an ARIA `combobox`/`listbox` pairing with roving keyboard navigation (↑/↓ to move,
 * Enter to pick, Escape to clear) and screen-reader labels (WCAG).
 */
@Component({
  selector: 'app-prefix-search',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    MatFormFieldModule,
    MatInputModule,
    MatIconModule,
    MatListModule,
    LoadingComponent,
    ErrorStateComponent,
    TranslatePipe,
  ],
  template: `
    <div class="dp-search">
      <mat-form-field class="dp-search__field" appearance="outline">
        <mat-label>{{ 'consumer.search.label' | translate }}</mat-label>
        <mat-icon matPrefix aria-hidden="true">search</mat-icon>
        <input
          #queryInput
          matInput
          type="text"
          role="combobox"
          aria-controls="dp-search-results"
          aria-autocomplete="list"
          [attr.aria-expanded]="hasResults()"
          [attr.aria-activedescendant]="activeId()"
          [placeholder]="'consumer.search.placeholder' | translate"
          [value]="value()"
          (input)="onInput(queryInput.value)"
          (keydown)="onKeydown($event)"
          data-testid="prefix-search-input"
        />
        @if (value().length > 0) {
          <button
            matSuffix
            mat-icon-button
            type="button"
            [attr.aria-label]="'consumer.search.clear' | translate"
            (click)="clearInput()"
            data-testid="prefix-search-clear"
          >
            <mat-icon aria-hidden="true">close</mat-icon>
          </button>
        }
      </mat-form-field>

      <div class="dp-search__panel">
        @if (loading()) {
          <app-loading [label]="'consumer.search.loading' | translate" />
        } @else if (error()) {
          <app-error-state [error]="error()" [retryable]="false" />
        } @else if (hasResults()) {
          <ul
            id="dp-search-results"
            class="dp-search__results"
            role="listbox"
            [attr.aria-label]="'consumer.search.results' | translate"
            data-testid="prefix-search-results"
          >
            @for (result of results(); track result.id; let i = $index) {
              <li
                role="option"
                class="dp-search__result"
                tabindex="-1"
                [id]="optionId(i)"
                [class.dp-search__result--active]="i === activeIndex()"
                [attr.aria-selected]="result.selected"
                (click)="emitPick(result)"
                (keydown.enter)="emitPick(result)"
                (keydown.space)="emitPick(result)"
                (mouseenter)="setActive(i)"
                [attr.data-testid]="'prefix-search-result-' + result.id"
              >
                <mat-icon aria-hidden="true">{{
                  result.kind === 'category' ? 'category' : 'restaurant_menu'
                }}</mat-icon>
                <span class="dp-search__result-label">{{ result.label }}</span>
                @if (result.selected) {
                  <mat-icon class="dp-search__result-check" aria-hidden="true">check</mat-icon>
                }
              </li>
            }
          </ul>
        } @else if (showEmpty()) {
          <app-error-state [emptyMessage]="'consumer.search.empty' | translate" />
        }
      </div>
    </div>
  `,
  styles: `
    .dp-search {
      display: flex;
      flex-direction: column;
      gap: var(--dp-space-2);
    }

    .dp-search__field {
      width: 100%;
    }

    .dp-search__results {
      list-style: none;
      margin: 0;
      padding: 0;
      border: var(--dp-border-width-hairline) solid var(--dp-color-outline-variant);
      border-radius: var(--dp-radius-md);
      overflow: hidden;
    }

    .dp-search__result {
      display: flex;
      align-items: center;
      gap: var(--dp-space-3);
      padding: var(--dp-space-3) var(--dp-space-4);
      cursor: pointer;
      color: var(--dp-color-on-surface);
    }

    .dp-search__result + .dp-search__result {
      border-top: var(--dp-border-width-hairline) solid var(--dp-color-outline-variant);
    }

    .dp-search__result--active {
      background-color: var(--dp-color-surface-variant);
    }

    .dp-search__result-label {
      flex: 1 1 auto;
    }

    .dp-search__result-check {
      color: var(--dp-color-primary);
    }
  `,
})
export class PrefixSearchComponent {
  /** The current matches the container fed back in for the active prefix. */
  readonly results = input<readonly PrefixSearchResult[]>([]);
  /** True while the container's prefix lookup is in flight. */
  readonly loading = input<boolean>(false);
  /** The typed error from the last failed lookup, or `undefined`. */
  readonly error = input<ApiError | undefined>(undefined);
  /** True once a non-trivial prefix was queried (so an empty result shows the empty state). */
  readonly queried = input<boolean>(false);

  /** Emits the raw input text on every change; the container debounces and runs the prefix search. */
  readonly query = output<string>();
  /** Emits the picked result so the container adds the matching category/dish to the selection. */
  readonly pick = output<PrefixSearchResult>();

  /** The current input text (mirrored locally so the clear button and keyboard work without a model). */
  private readonly text = signal<string>('');
  /** The roving active option index for keyboard navigation (−1 = none). */
  private readonly active = signal<number>(-1);

  readonly value = computed<string>(() => this.text());
  readonly activeIndex = computed<number>(() => this.active());
  readonly hasResults = computed<boolean>(() => this.results().length > 0);
  /** Show the empty-state only when a prefix was queried, nothing matched, and not loading/erroring. */
  readonly showEmpty = computed<boolean>(
    () => this.queried() && !this.loading() && this.error() === undefined && !this.hasResults(),
  );
  /** The id of the active option for `aria-activedescendant` (absent when none is active). */
  readonly activeId = computed<string | null>(() =>
    this.active() >= 0 ? this.optionId(this.active()) : null,
  );

  constructor() {
    // When the container feeds in a new result set, drop the roving selection so
    // `aria-activedescendant` never dangles on an option index that no longer matches (a11y).
    effect(() => {
      this.results();
      this.active.set(-1);
    });
  }

  /** Stable per-row id used by `aria-activedescendant` and the option `id`. */
  optionId(index: number): string {
    return `dp-search-option-${index}`;
  }

  /** Mirror the input text, reset the active row, and emit it for the container to debounce. */
  onInput(value: string): void {
    this.text.set(value);
    this.active.set(-1);
    this.query.emit(value);
  }

  /** Clear the input and tell the container to drop the current results. */
  clearInput(): void {
    this.text.set('');
    this.active.set(-1);
    this.query.emit('');
  }

  /** Hover/keyboard sets the active row (roving focus without moving DOM focus off the input). */
  setActive(index: number): void {
    this.active.set(index);
  }

  /** Emit the pick; keep the input so the user can keep adding from the same prefix. */
  emitPick(result: PrefixSearchResult): void {
    this.pick.emit(result);
  }

  /** Keyboard navigation: ↑/↓ move the active row, Enter picks it, Escape clears the input. */
  onKeydown(event: KeyboardEvent): void {
    const count = this.results().length;
    switch (event.key) {
      case 'ArrowDown':
        if (count > 0) {
          event.preventDefault();
          this.active.set((this.active() + 1) % count);
        }
        break;
      case 'ArrowUp':
        if (count > 0) {
          event.preventDefault();
          this.active.set((this.active() - 1 + count) % count);
        }
        break;
      case 'Enter': {
        const index = this.active();
        if (index >= 0 && index < count) {
          event.preventDefault();
          this.emitPick(this.results()[index]);
        }
        break;
      }
      case 'Escape':
        if (this.text().length > 0) {
          event.preventDefault();
          this.clearInput();
        }
        break;
      default:
        break;
    }
  }
}
