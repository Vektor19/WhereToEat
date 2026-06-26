import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import {
  FormBuilder,
  ReactiveFormsModule,
  Validators,
  type AbstractControl,
  type FormGroup,
  type ValidationErrors,
} from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { TranslatePipe } from '@ngx-translate/core';

import {
  CREATE_AD_PLACEMENT,
  LoadingService,
  type CreateAdPlacementBody,
  type CreatedAdPlacement,
} from 'core';

import { SaveFeedbackComponent } from '../shared/save-feedback.component';

/**
 * Cross-field validator: the placement window must be ordered (`startsAt` strictly before `endsAt`).
 * Returns `{ dateRange: true }` on the group when both ends are present and the start is not before
 * the end, so the form fails fast before the round-trip. Empty ends are left to the per-control
 * `required` validators.
 */
function dateRangeValidator(group: AbstractControl): ValidationErrors | null {
  const startsAt = group.get('startsAt')?.value as string;
  const endsAt = group.get('endsAt')?.value as string;
  if (!startsAt || !endsAt) {
    return null;
  }
  return new Date(startsAt).getTime() < new Date(endsAt).getTime() ? null : { dateRange: true };
}

/**
 * Labeled ad-placement container (smart) — Step 20 (monetization §7.2, invariant #10 — **critical
 * compliance**).
 *
 * An operator creates a targeted, time-bounded **paid promotional slot** for a venue via
 * `POST /admin/venues/{id}/ad-placements` with `{ targetingKey, startsAt, endsAt }`, returning
 * **201 Created** with `{ id }` (surfaced as a confirmation with the created id).
 *
 * **Compliance copy (invariant #10):** the UI frames the placement explicitly as a **paid, labeled
 * slot that is separate from organic ranking** — the organic rank is **not** sold; an ad slot is always
 * marked and visually separated in the consumer surface. The copy here never implies the placement
 * influences organic results.
 *
 * The window is validated client-side (`startsAt` strictly before `endsAt`) so the UI fails fast before
 * the round-trip. The local `datetime-local` values are converted to ISO-8601 strings for the wire body
 * (the backend binds `DateTimeOffset`). The shared {@link SaveFeedbackComponent} shows in-progress /
 * typed error; a dedicated confirmation strip shows the created id on **201**.
 *
 * This is the only place that touches the data-access port; the feedback strip is presentational.
 * Portal code never imports consumer internals; copy from the runtime catalogs; themed from tokens.
 */
@Component({
  selector: 'app-ad-placement',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    TranslatePipe,
    SaveFeedbackComponent,
  ],
  template: `
    <section class="dp-ads" data-testid="ad-placement">
      <header class="dp-ads__header">
        <h1 class="dp-ads__title">{{ 'portal.ads.title' | translate }}</h1>
        <p class="dp-ads__intro">{{ 'portal.ads.intro' | translate }}</p>
      </header>

      <p class="dp-ads__labeled-note" data-testid="ad-labeled-note">
        <mat-icon class="dp-ads__labeled-icon" aria-hidden="true">campaign</mat-icon>
        <span>{{ 'portal.ads.labeledNote' | translate }}</span>
      </p>

      <form class="dp-ads__form" data-testid="ad-form" [formGroup]="form" (ngSubmit)="create()">
        <mat-form-field appearance="outline">
          <mat-label>{{ 'portal.ads.venueId' | translate }}</mat-label>
          <mat-icon matIconPrefix aria-hidden="true">store</mat-icon>
          <input
            matInput
            type="text"
            autocomplete="off"
            formControlName="venueId"
            data-testid="ad-venue-input"
          />
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>{{ 'portal.ads.targetingKey' | translate }}</mat-label>
          <mat-icon matIconPrefix aria-hidden="true">ads_click</mat-icon>
          <input
            matInput
            type="text"
            autocomplete="off"
            formControlName="targetingKey"
            data-testid="ad-targeting-input"
          />
          <mat-hint>{{ 'portal.ads.targetingKeyHint' | translate }}</mat-hint>
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>{{ 'portal.ads.startsAt' | translate }}</mat-label>
          <input
            matInput
            type="datetime-local"
            formControlName="startsAt"
            data-testid="ad-starts-input"
          />
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>{{ 'portal.ads.endsAt' | translate }}</mat-label>
          <input
            matInput
            type="datetime-local"
            formControlName="endsAt"
            data-testid="ad-ends-input"
          />
        </mat-form-field>

        @if (form.errors?.['dateRange'] && form.controls['endsAt'].value) {
          <p class="dp-ads__range-error" role="alert" data-testid="ad-range-error">
            <mat-icon class="dp-ads__range-icon" aria-hidden="true">error_outline</mat-icon>
            <span>{{ 'portal.ads.rangeError' | translate }}</span>
          </p>
        }

        <button
          mat-flat-button
          color="primary"
          type="submit"
          class="dp-ads__create"
          data-testid="ad-create"
          [disabled]="form.invalid || saving()"
        >
          <mat-icon aria-hidden="true">add</mat-icon>
          {{ 'portal.ads.create' | translate }}
        </button>

        @if (created(); as placement) {
          <p class="dp-ads__created" role="status" data-testid="ad-created">
            <mat-icon class="dp-ads__created-icon" aria-hidden="true">check_circle</mat-icon>
            <span
              >{{ 'portal.ads.created' | translate }}
              <code data-testid="ad-created-id">{{ placement.id }}</code></span
            >
          </p>
        }

        <app-save-feedback [saving]="saving()" [saved]="false" [error]="saveError()" />
      </form>
    </section>
  `,
  styles: `
    :host {
      display: block;
    }

    .dp-ads {
      display: flex;
      flex-direction: column;
      gap: var(--dp-space-4);
    }

    .dp-ads__title {
      margin: 0;
      font-size: var(--dp-font-size-title);
      font-weight: var(--dp-font-weight-bold);
    }

    .dp-ads__intro {
      margin: var(--dp-space-1) 0 0;
      color: var(--dp-color-on-surface-variant);
    }

    .dp-ads__labeled-note {
      display: flex;
      align-items: flex-start;
      gap: var(--dp-space-2);
      margin: 0;
      color: var(--dp-color-on-surface-variant);
      font-size: var(--dp-font-size-caption);
    }

    .dp-ads__labeled-icon {
      flex: none;
      font-size: var(--dp-font-size-body);
      width: var(--dp-font-size-body);
      height: var(--dp-font-size-body);
    }

    .dp-ads__form {
      display: flex;
      flex-direction: column;
      gap: var(--dp-space-3);
      max-width: var(--dp-layout-content-max);
    }

    .dp-ads__range-error {
      display: flex;
      align-items: center;
      gap: var(--dp-space-2);
      margin: 0;
      color: var(--dp-color-error);
      font-size: var(--dp-font-size-caption);
    }

    .dp-ads__range-icon {
      flex: none;
    }

    .dp-ads__create {
      align-self: flex-start;
    }

    .dp-ads__created {
      display: flex;
      align-items: center;
      gap: var(--dp-space-2);
      margin: 0;
      color: var(--dp-color-primary);
      font-size: var(--dp-font-size-body);
    }

    .dp-ads__created-icon {
      flex: none;
    }
  `,
})
export class AdPlacementComponent {
  private readonly createAdPlacement = inject(CREATE_AD_PLACEMENT);
  private readonly loadingService = inject(LoadingService);
  private readonly fb = inject(FormBuilder);

  private readonly saveState = this.loadingService.create<CreatedAdPlacement>();

  readonly saving = this.saveState.loading;
  readonly saveError = this.saveState.error;
  /** The created placement (`{ id }`) once a **201** lands; drives the confirmation strip. */
  readonly created = this.saveState.data;

  /** Venue, targeting key, and an ordered window — the group validator enforces `startsAt < endsAt`. */
  readonly form: FormGroup = this.fb.group(
    {
      venueId: ['', Validators.required],
      targetingKey: ['', Validators.required],
      startsAt: ['', Validators.required],
      endsAt: ['', Validators.required],
    },
    { validators: dateRangeValidator },
  );

  /** Create the placement (`POST /admin/venues/{id}/ad-placements`); 201 `{ id }` → confirmation. */
  create(): void {
    if (this.form.invalid) {
      return;
    }
    const raw = this.form.getRawValue() as {
      venueId: string;
      targetingKey: string;
      startsAt: string;
      endsAt: string;
    };
    const body: CreateAdPlacementBody = {
      targetingKey: raw.targetingKey.trim(),
      startsAt: new Date(raw.startsAt).toISOString(),
      endsAt: new Date(raw.endsAt).toISOString(),
    };
    this.saveState.run(this.createAdPlacement.execute(raw.venueId.trim(), body));
  }
}
