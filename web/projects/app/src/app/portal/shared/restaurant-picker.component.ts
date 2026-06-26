import { ChangeDetectionStrategy, Component, input, output, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { TranslatePipe } from '@ngx-translate/core';

/**
 * Shared operator-portal restaurant picker (dumb / presentational) — Step 19.
 *
 * The small "enter a restaurant id, press Load" affordance the menu / protection / address features
 * reuse (DRY): there is **no dedicated admin read endpoint**, so an operator addresses a restaurant by
 * its opaque id and the host container fetches `GET /restaurants/{id}` to populate its form. This
 * component owns no data access — it validates that a non-blank id was entered and emits `load(id)`;
 * the smart container performs the fetch through the shared async-state primitive.
 *
 * It is presentational and view-only (an accessible Material field + button, label + ARIA + keyboard
 * for free), so it mirrors 1:1 onto an RN form. Copy resolves from the runtime i18n catalogs; the
 * `labelKey` is supplied by the host so each feature can phrase its target ("restaurant", etc.).
 */
@Component({
  selector: 'app-restaurant-picker',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatFormFieldModule, MatInputModule, MatButtonModule, MatIconModule, TranslatePipe],
  template: `
    <form class="dp-picker" data-testid="restaurant-picker" (submit)="onSubmit($event)">
      <mat-form-field appearance="outline" class="dp-picker__field">
        <mat-label>{{ labelKey() | translate }}</mat-label>
        <mat-icon matIconPrefix aria-hidden="true">store</mat-icon>
        <input
          matInput
          type="text"
          autocomplete="off"
          data-testid="restaurant-id-input"
          [value]="value()"
          (input)="onInput($event)"
        />
        <mat-hint>{{ 'portal.picker.hint' | translate }}</mat-hint>
      </mat-form-field>

      <button
        mat-flat-button
        color="primary"
        type="submit"
        class="dp-picker__load"
        data-testid="restaurant-load"
        [disabled]="loading() || value().length === 0"
      >
        <mat-icon aria-hidden="true">download</mat-icon>
        {{ 'portal.picker.load' | translate }}
      </button>
    </form>
  `,
  styles: `
    :host {
      display: block;
    }

    .dp-picker {
      display: flex;
      align-items: flex-start;
      gap: var(--dp-space-3);
      flex-wrap: wrap;
    }

    .dp-picker__field {
      flex: 1 1 var(--dp-field-basis-lg);
      min-width: var(--dp-field-basis-md);
    }

    .dp-picker__load {
      margin-top: var(--dp-space-1);
    }
  `,
})
export class RestaurantPickerComponent {
  /** i18n catalog key for the field label (the host phrases its target). */
  readonly labelKey = input<string>('portal.picker.restaurantLabel');
  /** True while the host's fetch is in flight (disables the load button). */
  readonly loading = input<boolean>(false);

  /** Emitted with the trimmed, non-blank id when the operator presses Load (named to avoid the
   * native `load` DOM event — `@angular-eslint/no-output-native`). */
  readonly loadId = output<string>();

  /** The current input value (trimmed on emit). */
  readonly value = signal<string>('');

  /** Track the raw input so the load button enables only for a non-blank id. */
  onInput(event: Event): void {
    this.value.set((event.target as HTMLInputElement).value.trim());
  }

  /** Submit the entered id (the field's Enter key and the Load button both route here). */
  onSubmit(event: Event): void {
    event.preventDefault();
    const id = this.value();
    if (id.length === 0) {
      return;
    }
    this.loadId.emit(id);
  }
}
