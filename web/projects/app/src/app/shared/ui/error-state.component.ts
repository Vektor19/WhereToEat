import { ChangeDetectionStrategy, Component, computed, inject, input, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { translateText, type ApiError } from 'core';

/**
 * Shared error / empty-state panel (dumb / presentational).
 *
 * The one error-and-empty UI the features reuse so failures and empty results look the same
 * everywhere (DRY). It renders from the typed {@link ApiError} the error-normalization interceptor
 * (Step 7) produced — **feature code never hands it a raw `HttpErrorResponse`** — and exposes an
 * optional `retry` output a smart container wires to re-run its request.
 *
 * The user-facing copy is resolved from the error's i18n `messageKey` through the runtime catalogs
 * (Step 17): the old inline `uk` fallback map was folded into the `errors.*` catalog entries, so this
 * component holds **no launch-locale strings**. When `error` is absent the component renders the
 * empty-state copy instead, so one component covers both the "failed" and "loaded but empty" cases.
 */
@Component({
  selector: 'app-error-state',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule, MatIconModule, TranslatePipe],
  template: `
    <div class="dp-error-state" role="alert" data-testid="error-state">
      <mat-icon class="dp-error-state__icon" aria-hidden="true">{{ icon() }}</mat-icon>
      <p class="dp-error-state__message">{{ message() }}</p>
      @if (canRetry()) {
        <button mat-stroked-button type="button" data-testid="error-retry" (click)="retry.emit()">
          {{ 'common.retry' | translate }}
        </button>
      }
    </div>
  `,
  styles: `
    .dp-error-state {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: var(--dp-space-3);
      padding: var(--dp-space-6) var(--dp-space-4);
      text-align: center;
      color: var(--dp-color-on-surface-variant);
    }

    .dp-error-state__icon {
      color: var(--dp-color-error);
    }

    .dp-error-state__message {
      margin: 0;
      font-size: var(--dp-font-size-body);
    }
  `,
})
export class ErrorStateComponent {
  private readonly translate = inject(TranslateService);

  /** The typed error to render. Absent → the component renders the empty-state copy instead. */
  readonly error = input<ApiError | undefined>(undefined);
  /**
   * Copy shown when there is no error but the loaded result is empty — a resolved string the container
   * passes (typically from a catalog key). Defaults to the generic "nothing found" catalog copy.
   */
  readonly emptyMessage = input<string | undefined>(undefined);
  /** Whether to offer a retry affordance (only meaningful for the error case). */
  readonly retryable = input<boolean>(true);

  /** Emitted when the user clicks retry. */
  readonly retry = output<void>();

  /** The generic empty-state copy, used when the container did not pass an `emptyMessage`. */
  private readonly defaultEmpty = translateText(this.translate, 'common.nothingFound');

  /**
   * The error's localized message, resolved reactively from its `messageKey` (e.g. `errors.notFound`).
   * The key arrow tracks `error()` and the returned signal re-resolves on a language switch.
   */
  private readonly errorMessage = translateText(
    this.translate,
    () => this.error()?.messageKey ?? 'errors.unknown',
  );

  /** Resolve the user-facing message: the localized error copy, or the empty-state copy. */
  readonly message = computed<string>(() => {
    if (this.error() === undefined) {
      return this.emptyMessage() ?? this.defaultEmpty();
    }
    return this.errorMessage();
  });

  /** Icon: an error glyph for a failure, an empty glyph otherwise. */
  readonly icon = computed<string>(() => (this.error() === undefined ? 'inbox' : 'error_outline'));

  /** Retry only for an actual error and when allowed. */
  readonly canRetry = computed<boolean>(() => this.error() !== undefined && this.retryable());
}
