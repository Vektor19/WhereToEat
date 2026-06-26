import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators, type FormGroup } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { TranslatePipe } from '@ngx-translate/core';

import { GRANT_VERIFIED, LoadingService, REVOKE_VERIFIED, type VerifiedTier } from 'core';

import { SaveFeedbackComponent } from '../shared/save-feedback.component';

/**
 * Verified-subscription container (smart) — Step 20 (monetization §7.1, invariant #10).
 *
 * An operator grants a venue a **Verified** tier (`Basic` / `Pro`) via `PUT /admin/venues/{id}/verified`
 * — the tier rides as the backend `SubscriptionTier` integer ordinal (`Basic=1`, `Pro=2`), which the
 * data-access port serializes from the readable {@link VerifiedTier} union — or **revokes** it via
 * `DELETE /admin/venues/{id}/verified`. Both treat **204** as success.
 *
 * **Compliance copy (invariant #10 / §7.1):** the Verified badge is framed strictly as a
 * **partner / confirmed-status** marker (the venue claimed its card and may add real photos / owner
 * menu), **not** a "quality seal from us". The organic rating is never affected by Verified status.
 * There is **no billing UI** here — payment is a server-side no-op seam; this is the management action
 * only.
 *
 * There is no admin read of a venue's current tier (no dedicated read endpoint), so the operator
 * addresses the venue by its opaque id and the grant is an explicit **set** of the chosen tier; the
 * shared {@link SaveFeedbackComponent} shows in-progress / saved / typed error for whichever action ran.
 *
 * This is the only place that touches the data-access ports; the feedback strip is presentational.
 * Portal code never imports consumer internals; copy from the runtime catalogs; themed from tokens.
 */
@Component({
  selector: 'app-verified',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    TranslatePipe,
    SaveFeedbackComponent,
  ],
  template: `
    <section class="dp-verified" data-testid="verified">
      <header class="dp-verified__header">
        <h1 class="dp-verified__title">{{ 'portal.verified.title' | translate }}</h1>
        <p class="dp-verified__intro">{{ 'portal.verified.intro' | translate }}</p>
      </header>

      <p class="dp-verified__partner-note" data-testid="verified-partner-note">
        <mat-icon class="dp-verified__partner-icon" aria-hidden="true">info</mat-icon>
        <span>{{ 'portal.verified.partnerNote' | translate }}</span>
      </p>

      <form class="dp-verified__form" data-testid="verified-form" [formGroup]="form">
        <mat-form-field appearance="outline">
          <mat-label>{{ 'portal.verified.venueId' | translate }}</mat-label>
          <mat-icon matIconPrefix aria-hidden="true">store</mat-icon>
          <input
            matInput
            type="text"
            autocomplete="off"
            formControlName="venueId"
            data-testid="verified-venue-input"
          />
          <mat-hint>{{ 'portal.verified.venueIdHint' | translate }}</mat-hint>
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>{{ 'portal.verified.tier' | translate }}</mat-label>
          <mat-select formControlName="tier" data-testid="verified-tier-select">
            <mat-option value="Basic">{{ 'portal.verified.tierBasic' | translate }}</mat-option>
            <mat-option value="Pro">{{ 'portal.verified.tierPro' | translate }}</mat-option>
          </mat-select>
          <mat-hint>{{ 'portal.verified.tierHint' | translate }}</mat-hint>
        </mat-form-field>

        <div class="dp-verified__actions">
          <button
            mat-flat-button
            color="primary"
            type="button"
            data-testid="verified-grant"
            [disabled]="form.invalid || saving()"
            (click)="grant()"
          >
            <mat-icon aria-hidden="true">verified</mat-icon>
            {{ 'portal.verified.grant' | translate }}
          </button>

          <button
            mat-stroked-button
            type="button"
            data-testid="verified-revoke"
            [disabled]="venueIdBlank() || saving()"
            (click)="revoke()"
          >
            <mat-icon aria-hidden="true">remove_moderator</mat-icon>
            {{ 'portal.verified.revoke' | translate }}
          </button>
        </div>

        <app-save-feedback [saving]="saving()" [saved]="saved()" [error]="saveError()" />
      </form>
    </section>
  `,
  styles: `
    :host {
      display: block;
    }

    .dp-verified {
      display: flex;
      flex-direction: column;
      gap: var(--dp-space-4);
    }

    .dp-verified__title {
      margin: 0;
      font-size: var(--dp-font-size-title);
      font-weight: var(--dp-font-weight-bold);
    }

    .dp-verified__intro {
      margin: var(--dp-space-1) 0 0;
      color: var(--dp-color-on-surface-variant);
    }

    .dp-verified__partner-note {
      display: flex;
      align-items: flex-start;
      gap: var(--dp-space-2);
      margin: 0;
      color: var(--dp-color-on-surface-variant);
      font-size: var(--dp-font-size-caption);
    }

    .dp-verified__partner-icon {
      flex: none;
      font-size: var(--dp-font-size-body);
      width: var(--dp-font-size-body);
      height: var(--dp-font-size-body);
    }

    .dp-verified__form {
      display: flex;
      flex-direction: column;
      gap: var(--dp-space-3);
      max-width: var(--dp-layout-content-max);
    }

    .dp-verified__actions {
      display: flex;
      gap: var(--dp-space-3);
      flex-wrap: wrap;
    }
  `,
})
export class VerifiedComponent {
  private readonly grantVerified = inject(GRANT_VERIFIED);
  private readonly revokeVerified = inject(REVOKE_VERIFIED);
  private readonly loadingService = inject(LoadingService);
  private readonly fb = inject(FormBuilder);

  private readonly saveState = this.loadingService.create<void>();

  readonly saving = this.saveState.loading;
  readonly saved = this.saveState.loaded;
  readonly saveError = this.saveState.error;

  /** Venue id required for any action; tier required for a grant (defaults to `Basic`). */
  readonly form: FormGroup = this.fb.group({
    venueId: ['', Validators.required],
    tier: ['Basic' as VerifiedTier, Validators.required],
  });

  /** True when the venue-id field is blank — revoke needs only the id, so it gates on this alone. */
  venueIdBlank(): boolean {
    const venueId = this.form.controls['venueId'].value as string;
    return venueId.trim().length === 0;
  }

  /** Grant the chosen tier (`PUT /admin/venues/{id}/verified`, ordinal in the body); 204 → saved. */
  grant(): void {
    if (this.form.invalid) {
      return;
    }
    const raw = this.form.getRawValue() as { venueId: string; tier: VerifiedTier };
    this.saveState.run(this.grantVerified.execute(raw.venueId.trim(), raw.tier));
  }

  /** Revoke the venue's Verified status (`DELETE /admin/venues/{id}/verified`); 204 → saved. */
  revoke(): void {
    if (this.venueIdBlank()) {
      return;
    }
    const venueId = (this.form.controls['venueId'].value as string).trim();
    this.saveState.run(this.revokeVerified.execute(venueId));
  }
}
