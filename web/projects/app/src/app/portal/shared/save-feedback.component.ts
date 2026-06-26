import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { translateText, type ApiError } from 'core';

/**
 * Shared operator-portal save-feedback panel (dumb / presentational) — Step 19.
 *
 * The one in-progress / success / failure strip every portal management feature reuses (DRY) so an
 * admin write looks the same everywhere: while the write is in flight it shows a discreet spinner,
 * on a **204** it shows the success copy, and on a failure it renders the localized message from the
 * typed {@link ApiError} the error-normalization interceptor (Step 7) produced — feature code never
 * hands it a raw `HttpErrorResponse`.
 *
 * It is deliberately distinct from the consumer {@link ErrorStateComponent}: that one is a
 * full-panel "load failed / empty" surface with a retry affordance for a **read**, whereas this is an
 * inline result strip for a **write** that additionally surfaces the saved-OK state. It lives under
 * `portal/shared` so it never crosses into the consumer surface (the import-boundary rules keep the
 * two apart). Copy resolves from the runtime i18n catalogs (Step 17); themed from design tokens.
 */
@Component({
  selector: 'app-save-feedback',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatIconModule, MatProgressSpinnerModule, TranslatePipe],
  template: `
    @if (saving()) {
      <p class="dp-save dp-save--pending" data-testid="save-pending">
        <mat-progress-spinner
          mode="indeterminate"
          diameter="18"
          [attr.aria-label]="'portal.save.saving' | translate"
        />
        <span>{{ 'portal.save.saving' | translate }}</span>
      </p>
    } @else if (error(); as err) {
      <p class="dp-save dp-save--error" role="alert" data-testid="save-error">
        <mat-icon class="dp-save__icon" aria-hidden="true">error_outline</mat-icon>
        <span [attr.data-message-key]="err.messageKey">{{ errorText() }}</span>
      </p>
    } @else if (saved()) {
      <p class="dp-save dp-save--ok" role="status" data-testid="save-success">
        <mat-icon class="dp-save__icon" aria-hidden="true">check_circle</mat-icon>
        <span>{{ 'portal.save.success' | translate }}</span>
      </p>
    }
  `,
  styles: `
    :host {
      display: block;
    }

    .dp-save {
      display: flex;
      align-items: center;
      gap: var(--dp-space-2);
      margin: 0;
      font-size: var(--dp-font-size-body);
    }

    .dp-save--ok {
      color: var(--dp-color-primary);
    }

    .dp-save--error {
      color: var(--dp-color-error);
    }

    .dp-save--pending {
      color: var(--dp-color-on-surface-variant);
    }

    .dp-save__icon {
      flex: none;
    }
  `,
})
export class SaveFeedbackComponent {
  private readonly translate = inject(TranslateService);

  /** True while the admin write is in flight. */
  readonly saving = input<boolean>(false);
  /** True once the write returned 204 (and no subsequent failure). */
  readonly saved = input<boolean>(false);
  /** The typed error from the last failed write, or `undefined`. */
  readonly error = input<ApiError | undefined>(undefined);

  /**
   * The error's localized message, resolved reactively from its `messageKey` (e.g. `errors.notFound`).
   * The key arrow tracks `error()` so the copy re-resolves on a language switch.
   */
  private readonly resolvedError = translateText(
    this.translate,
    () => this.error()?.messageKey ?? 'errors.unknown',
  );

  /** The localized failure copy (only read when {@link error} is set). */
  readonly errorText = computed<string>(() => this.resolvedError());
}
