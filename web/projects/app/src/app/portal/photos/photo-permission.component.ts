import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators, type FormGroup } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { TranslatePipe } from '@ngx-translate/core';

import { LoadingService, SET_PHOTO_PERMISSION } from 'core';

import { SaveFeedbackComponent } from '../shared/save-feedback.component';

/**
 * Photo-permission container (smart) — Step 19 (invariant #8). Our own **generic** category photos
 * stay the default everywhere; a real photo is shown **only** for a venue that granted permission. This
 * feature toggles that per-photo permission gate via `PUT /admin/photos/{id}/permission` (the `{ value }`
 * body), treating **204** as success.
 *
 * There is no admin read of a photo's current permission (no dedicated admin read endpoint), so the
 * operator addresses a photo by its opaque id and the toggle is an explicit **set**: the chosen value
 * is sent and the result strip shows in-progress / saved / typed error. The copy makes the
 * generic-by-default rule explicit so an operator understands what the gate grants.
 *
 * This is the only place that touches the data-access port; the feedback strip is presentational.
 * Portal code never imports consumer internals; copy from the runtime catalogs; themed from tokens.
 */
@Component({
  selector: 'app-photo-permission',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatFormFieldModule,
    MatInputModule,
    MatSlideToggleModule,
    MatButtonModule,
    MatIconModule,
    TranslatePipe,
    SaveFeedbackComponent,
  ],
  template: `
    <section class="dp-photos" data-testid="photo-permission">
      <header class="dp-photos__header">
        <h1 class="dp-photos__title">{{ 'portal.photos.title' | translate }}</h1>
        <p class="dp-photos__intro">{{ 'portal.photos.intro' | translate }}</p>
      </header>

      <form class="dp-photos__form" data-testid="photo-form" [formGroup]="form" (ngSubmit)="save()">
        <mat-form-field appearance="outline">
          <mat-label>{{ 'portal.photos.photoId' | translate }}</mat-label>
          <mat-icon matIconPrefix aria-hidden="true">image</mat-icon>
          <input
            matInput
            type="text"
            autocomplete="off"
            formControlName="photoId"
            data-testid="photo-id-input"
          />
          <mat-hint>{{ 'portal.photos.photoIdHint' | translate }}</mat-hint>
        </mat-form-field>

        <mat-slide-toggle formControlName="value" data-testid="photo-permission-toggle">
          {{ 'portal.photos.permission' | translate }}
        </mat-slide-toggle>
        <p class="dp-photos__hint">{{ 'portal.photos.permissionHint' | translate }}</p>

        <button
          mat-flat-button
          color="primary"
          type="submit"
          class="dp-photos__save"
          data-testid="photo-save"
          [disabled]="form.invalid || saving()"
        >
          <mat-icon aria-hidden="true">save</mat-icon>
          {{ 'portal.photos.save' | translate }}
        </button>

        <app-save-feedback [saving]="saving()" [saved]="saved()" [error]="saveError()" />
      </form>
    </section>
  `,
  styles: `
    :host {
      display: block;
    }

    .dp-photos {
      display: flex;
      flex-direction: column;
      gap: var(--dp-space-4);
    }

    .dp-photos__title {
      margin: 0;
      font-size: var(--dp-font-size-title);
      font-weight: var(--dp-font-weight-bold);
    }

    .dp-photos__intro {
      margin: var(--dp-space-1) 0 0;
      color: var(--dp-color-on-surface-variant);
    }

    .dp-photos__form {
      display: flex;
      flex-direction: column;
      gap: var(--dp-space-3);
      max-width: var(--dp-layout-content-max);
    }

    .dp-photos__hint {
      margin: 0;
      color: var(--dp-color-on-surface-variant);
      font-size: var(--dp-font-size-caption);
    }

    .dp-photos__save {
      align-self: flex-start;
    }
  `,
})
export class PhotoPermissionComponent {
  private readonly setPhotoPermission = inject(SET_PHOTO_PERMISSION);
  private readonly loadingService = inject(LoadingService);
  private readonly fb = inject(FormBuilder);

  private readonly saveState = this.loadingService.create<void>();

  readonly saving = this.saveState.loading;
  readonly saved = this.saveState.loaded;
  readonly saveError = this.saveState.error;

  /** Photo id required; `value` is the permission to set (default off = generic stays the default). */
  readonly form: FormGroup = this.fb.group({
    photoId: ['', Validators.required],
    value: [false],
  });

  /** Send the chosen permission for the entered photo (`PUT /admin/photos/{id}/permission`); 204 → saved. */
  save(): void {
    if (this.form.invalid) {
      return;
    }
    const raw = this.form.getRawValue() as { photoId: string; value: boolean };
    this.saveState.run(this.setPhotoPermission.execute(raw.photoId.trim(), raw.value));
  }
}
