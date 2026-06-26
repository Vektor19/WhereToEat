import { ChangeDetectionStrategy, Component, effect, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators, type FormGroup } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { TranslatePipe } from '@ngx-translate/core';

import {
  EDIT_ADDRESS,
  GET_RESTAURANT_DETAILS,
  LoadingService,
  type EditAddressBody,
  type RestaurantDetailsDto,
} from 'core';

import { LoadingComponent } from '../../shared/ui/loading.component';
import { ErrorStateComponent } from '../../shared/ui/error-state.component';
import { RestaurantPickerComponent } from '../shared/restaurant-picker.component';
import { SaveFeedbackComponent } from '../shared/save-feedback.component';

/**
 * Address-management container (smart) — Step 19. An operator edits a restaurant's address line +
 * optional city and persists via `PUT /admin/restaurants/{id}/address`, which **triggers a server-side
 * re-geocode** (the backend re-derives the OSM/Nominatim coordinates we store ourselves — invariant
 * #7; the client never sends or stores coordinates). A **204** is success.
 *
 * The restaurant is addressed by id through the shared picker, then the form is pre-filled from the
 * same public `GET /restaurants/{id}` read the consumer details use (there is no dedicated admin read).
 * The address line is required; the city is optional and sent as `null` when blank. Success / typed
 * failure surface through the shared {@link SaveFeedbackComponent}.
 *
 * This is the only place that touches the data-access ports; the picker / feedback strip are
 * presentational. Portal code never imports consumer internals; copy from the runtime catalogs; tokens.
 */
@Component({
  selector: 'app-address-management',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    TranslatePipe,
    LoadingComponent,
    ErrorStateComponent,
    RestaurantPickerComponent,
    SaveFeedbackComponent,
  ],
  template: `
    <section class="dp-address" data-testid="address-management">
      <header class="dp-address__header">
        <h1 class="dp-address__title">{{ 'portal.address.title' | translate }}</h1>
        <p class="dp-address__intro">{{ 'portal.address.intro' | translate }}</p>
      </header>

      <app-restaurant-picker
        labelKey="portal.address.restaurantLabel"
        [loading]="loading()"
        (loadId)="loadRestaurant($event)"
      />

      @if (loading()) {
        <app-loading />
      } @else if (error() !== undefined) {
        <app-error-state [error]="error()" [retryable]="true" (retry)="reload()" />
      } @else if (restaurant(); as r) {
        <form
          class="dp-address__form"
          data-testid="address-form"
          [formGroup]="form"
          (ngSubmit)="save(r.id)"
        >
          <mat-form-field appearance="outline">
            <mat-label>{{ 'portal.address.line' | translate }}</mat-label>
            <mat-icon matIconPrefix aria-hidden="true">signpost</mat-icon>
            <input
              matInput
              type="text"
              autocomplete="off"
              formControlName="addressLine"
              data-testid="address-line-input"
            />
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>{{ 'portal.address.city' | translate }}</mat-label>
            <mat-icon matIconPrefix aria-hidden="true">location_city</mat-icon>
            <input
              matInput
              type="text"
              autocomplete="off"
              formControlName="city"
              data-testid="address-city-input"
            />
          </mat-form-field>

          <p class="dp-address__regeocode">
            <mat-icon class="dp-address__regeocode-icon" aria-hidden="true">info</mat-icon>
            <span>{{ 'portal.address.regeocodeNote' | translate }}</span>
          </p>

          <button
            mat-flat-button
            color="primary"
            type="submit"
            class="dp-address__save"
            data-testid="address-save"
            [disabled]="form.invalid || saving()"
          >
            <mat-icon aria-hidden="true">save</mat-icon>
            {{ 'portal.address.save' | translate }}
          </button>

          <app-save-feedback [saving]="saving()" [saved]="saved()" [error]="saveError()" />
        </form>
      }
    </section>
  `,
  styles: `
    :host {
      display: block;
    }

    .dp-address {
      display: flex;
      flex-direction: column;
      gap: var(--dp-space-4);
    }

    .dp-address__title {
      margin: 0;
      font-size: var(--dp-font-size-title);
      font-weight: var(--dp-font-weight-bold);
    }

    .dp-address__intro {
      margin: var(--dp-space-1) 0 0;
      color: var(--dp-color-on-surface-variant);
    }

    .dp-address__form {
      display: flex;
      flex-direction: column;
      gap: var(--dp-space-3);
      max-width: var(--dp-layout-content-max);
    }

    .dp-address__regeocode {
      display: flex;
      align-items: flex-start;
      gap: var(--dp-space-2);
      margin: 0;
      color: var(--dp-color-on-surface-variant);
      font-size: var(--dp-font-size-caption);
    }

    .dp-address__regeocode-icon {
      flex: none;
      font-size: var(--dp-font-size-body);
      width: var(--dp-font-size-body);
      height: var(--dp-font-size-body);
    }

    .dp-address__save {
      align-self: flex-start;
    }
  `,
})
export class AddressManagementComponent {
  private readonly getDetails = inject(GET_RESTAURANT_DETAILS);
  private readonly editAddress = inject(EDIT_ADDRESS);
  private readonly loadingService = inject(LoadingService);
  private readonly fb = inject(FormBuilder);

  private readonly detailsState = this.loadingService.create<RestaurantDetailsDto>();
  private readonly saveState = this.loadingService.create<void>();
  private readonly restaurantId = signal<string | null>(null);

  // ── Bound async-state ─────────────────────────────────────────────────────
  readonly loading = this.detailsState.loading;
  readonly error = this.detailsState.error;
  readonly restaurant = this.detailsState.data;
  readonly saving = this.saveState.loading;
  readonly saved = this.saveState.loaded;
  readonly saveError = this.saveState.error;

  /** Address-line required; city optional (sent as `null` when blank). */
  readonly form: FormGroup = this.fb.group({
    addressLine: ['', [Validators.required, Validators.maxLength(256)]],
    city: [''],
  });

  constructor() {
    // Pre-fill the form whenever a restaurant's details land.
    effect(() => {
      const dto = this.restaurant();
      if (dto !== undefined) {
        this.form.reset({ addressLine: dto.addressLine, city: dto.addressCity ?? '' });
      }
    });
  }

  /** Fetch the restaurant's details to pre-fill the address. Resets any prior save state. */
  loadRestaurant(id: string): void {
    this.restaurantId.set(id);
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

  /** Persist the address (`PUT /admin/restaurants/{id}/address`, triggers re-geocode); 204 → saved. */
  save(restaurantId: string): void {
    if (this.form.invalid) {
      return;
    }
    const raw = this.form.getRawValue() as { addressLine: string; city: string };
    const city = raw.city.trim();
    const body: EditAddressBody = {
      addressLine: raw.addressLine.trim(),
      city: city.length > 0 ? city : null,
    };
    this.saveState.run(this.editAddress.execute(restaurantId, body));
  }
}
