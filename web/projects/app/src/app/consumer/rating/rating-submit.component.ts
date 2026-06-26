import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { tap } from 'rxjs/operators';

import {
  AnalyticsEmitterService,
  AnalyticsEventBuilders,
  AuthService,
  LoadingService,
  SUBMIT_RATING,
  translateText,
} from 'core';

/** The 1..5 star scores the input offers, ascending (rendered as a radiogroup). */
const SCORES: readonly number[] = [1, 2, 3, 4, 5];

/**
 * Rating-submit control (smart) — the consumer rating UI (§5.6, invariants #6 / #11). It lets a
 * **signed-in** user submit (or revise) a 1..5 score for a restaurant through the authenticated
 * {@link SUBMIT_RATING} Public-host operation (the Step 6 bearer interceptor attaches the token because
 * the path ends with the `/ratings` allowlist suffix).
 *
 * Honesty about the displayed value (invariant #6): the score the user picks is **not** shown back as
 * "their rating". The control is explicit that the public figure is our **smoothed/cumulative all-time**
 * rating, which the backend recomputes on the next pass after this submit feeds it — the
 * `smoothedNote` copy says exactly that. The displayed smoothed rating itself lives in the result card /
 * details surfaces; this control only feeds the backend.
 *
 * Sign-in gate (invariant #11 — the rating is tied to an opaque user ref server-side, never PII): when
 * the user is not authenticated the control shows a discreet sign-in prompt wired to {@link AuthService}
 * instead of the star input; only an authenticated user can pick a score and submit.
 *
 * Analytics: on a **successful** submit it emits one `rating_given` event through the Step 16 batching
 * {@link AnalyticsEmitterService} (using the Step 8 `ratingGiven` builder) — closing the `rating_given`
 * slot Step 16 left ready. The event carries only the restaurant id + the opaque session id (no `geo`
 * kind, no PII — invariant #11); the backend resolves identity from the bearer and anonymizes it.
 *
 * a11y: the star input is a `radiogroup` of five `radio` buttons — each star is a real `<button>` so it
 * is keyboard-focusable and Enter/Space-activatable for free, with `aria-checked` reflecting the
 * selection and an accessible name ("N з 5"). Material 3 + design tokens; standalone, OnPush, signals.
 */
@Component({
  selector: 'app-rating-submit',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule, MatIconModule, MatProgressSpinnerModule, TranslatePipe],
  template: `
    <section
      class="dp-rating"
      data-testid="rating-submit"
      [attr.aria-label]="'consumer.rating.region' | translate"
    >
      <h2 class="dp-rating__title">{{ 'consumer.rating.title' | translate }}</h2>

      @if (signedIn()) {
        <div
          class="dp-rating__stars"
          role="radiogroup"
          data-testid="rating-stars"
          [attr.aria-label]="'consumer.rating.title' | translate"
        >
          @for (score of scores; track score) {
            <button
              type="button"
              mat-icon-button
              class="dp-rating__star"
              role="radio"
              [class.dp-rating__star--on]="score <= (selected() ?? 0)"
              [attr.data-score]="score"
              [attr.aria-checked]="selected() === score"
              [attr.aria-label]="starLabel(score)"
              [disabled]="submitting()"
              (click)="select(score)"
            >
              <mat-icon aria-hidden="true">{{
                score <= (selected() ?? 0) ? 'star' : 'star_border'
              }}</mat-icon>
            </button>
          }
        </div>

        <button
          type="button"
          mat-flat-button
          color="primary"
          class="dp-rating__submit"
          data-testid="rating-submit-button"
          [disabled]="selected() === null || submitting()"
          (click)="submit()"
        >
          @if (submitting()) {
            <mat-progress-spinner
              class="dp-rating__spinner"
              mode="indeterminate"
              diameter="18"
              [attr.aria-label]="'consumer.rating.submitting' | translate"
            />
          }
          {{ 'consumer.rating.submit' | translate }}
        </button>

        @if (selected() === null) {
          <p class="dp-rating__hint" data-testid="rating-choose-hint">
            {{ 'consumer.rating.chooseScore' | translate }}
          </p>
        }

        @if (succeeded()) {
          <p class="dp-rating__success" role="status" data-testid="rating-success">
            {{ 'consumer.rating.success' | translate }}
          </p>
        }

        @if (errorMessageKey(); as key) {
          <p
            class="dp-rating__error"
            role="alert"
            data-testid="rating-error"
            [attr.data-message-key]="key"
          >
            {{ errorText() }}
          </p>
        }

        <p class="dp-rating__note" data-testid="rating-smoothed-note">
          <mat-icon class="dp-rating__note-icon" aria-hidden="true">info</mat-icon>
          <span>{{ 'consumer.rating.smoothedNote' | translate }}</span>
        </p>
      } @else {
        <p class="dp-rating__signin" data-testid="rating-signin">
          <span>{{ 'consumer.rating.signInPrompt' | translate }}</span>
          <button
            type="button"
            mat-stroked-button
            class="dp-rating__signin-button"
            data-testid="rating-signin-button"
            (click)="signIn()"
          >
            {{ 'consumer.rating.signIn' | translate }}
          </button>
        </p>
      }
    </section>
  `,
  styles: `
    .dp-rating {
      display: flex;
      flex-direction: column;
      gap: var(--dp-space-3);
    }

    .dp-rating__title {
      margin: 0;
      font-size: var(--dp-font-size-title);
      font-weight: var(--dp-font-weight-bold);
    }

    .dp-rating__stars {
      display: flex;
      gap: var(--dp-space-1);
    }

    .dp-rating__star--on {
      color: var(--dp-color-primary);
    }

    .dp-rating__submit {
      align-self: flex-start;
    }

    .dp-rating__success {
      margin: 0;
      color: var(--dp-color-primary);
      font-size: var(--dp-font-size-body);
    }

    .dp-rating__error {
      margin: 0;
      color: var(--dp-color-error);
      font-size: var(--dp-font-size-caption);
    }

    .dp-rating__hint {
      margin: 0;
      color: var(--dp-color-on-surface-variant);
      font-size: var(--dp-font-size-caption);
    }

    .dp-rating__note,
    .dp-rating__signin {
      display: flex;
      align-items: flex-start;
      gap: var(--dp-space-2);
      margin: 0;
      color: var(--dp-color-on-surface-variant);
      font-size: var(--dp-font-size-caption);
    }

    .dp-rating__note-icon {
      flex: none;
      font-size: var(--dp-font-size-body);
      width: var(--dp-font-size-body);
      height: var(--dp-font-size-body);
    }
  `,
})
export class RatingSubmitComponent {
  private readonly submitRating = inject(SUBMIT_RATING);
  private readonly auth = inject(AuthService);
  private readonly events = inject(AnalyticsEventBuilders);
  private readonly emitter = inject(AnalyticsEmitterService);
  private readonly loadingService = inject(LoadingService);
  private readonly translate = inject(TranslateService);

  /** The restaurant being rated. Required input from the host (details / result card). */
  readonly restaurantId = input.required<string>();

  /** The 1..5 scores rendered as the star radiogroup. */
  readonly scores = SCORES;

  /** The currently-selected score, or `null` before the user picks one. */
  readonly selected = signal<number | null>(null);

  /** Whether the last submit succeeded (reflects the submitted rating). */
  readonly succeeded = signal<boolean>(false);

  /** The submit request's async state (drives the spinner + typed error). */
  private readonly state = this.loadingService.create<void>();

  /** True when the user holds a valid session (only then can they rate). */
  readonly signedIn = this.auth.isAuthenticated;

  /** True while the submit POST is in flight. */
  readonly submitting = this.state.loading;

  /** The typed-error message key from a failed submit (e.g. `errors.validation`), or `null`. */
  readonly errorMessageKey = computed<string | null>(() => this.state.error()?.messageKey ?? null);

  /** Resolve the current error key to localized copy (re-resolves on a language switch). */
  private readonly resolvedError = translateText(
    this.translate,
    () => this.errorMessageKey() ?? 'common.empty',
  );

  /** The localized failure copy, or the empty string when there is no error. */
  readonly errorText = computed<string>(() =>
    this.errorMessageKey() === null ? '' : this.resolvedError(),
  );

  /** Accessible name for a star button ("N з 5"). */
  starLabel(score: number): string {
    return this.translate.instant('consumer.rating.star', { score });
  }

  /** Pick a score (clears any prior success/error so the UI reflects the new attempt). */
  select(score: number): void {
    this.selected.set(score);
    this.succeeded.set(false);
    // Clear a stale typed error from a prior failed submit so the new attempt starts clean —
    // otherwise the old 400 alert lingers until the next Submit (reset() returns the state to idle).
    this.state.reset();
  }

  /**
   * Submit the selected score. No-op when nothing is selected or a submit is already in flight. On
   * success it reflects the submitted rating and emits one `rating_given` analytics event (best-effort);
   * on failure the typed error surfaces localized. The displayed smoothed rating updates on the backend's
   * next recompute (invariant #6) — this control does not echo the raw score as "your rating".
   */
  submit(): void {
    const score = this.selected();
    if (score === null || this.submitting()) {
      return;
    }
    const restaurantId = this.restaurantId();
    this.succeeded.set(false);

    // Drive the shared async-state (loading + typed error) from the POST, and on the success
    // notification (a 204) reflect the submitted rating + emit the `rating_given` event. The `tap`
    // success side-effect runs only when the POST emits (never on error), so a failed submit reflects
    // the typed error and emits nothing. `state.run` lands on `loaded`/`error` from the same stream.
    this.state.run(
      this.submitRating.execute(restaurantId, score).pipe(
        tap(() => {
          this.succeeded.set(true);
          // Best-effort, non-blocking: one rating_given event — restaurant id + session id only (#11).
          this.emitter.emit(this.events.ratingGiven({ restaurantId }));
        }),
      ),
    );
  }

  /** Begin (or re-run) the sign-in flow so an anonymous user can rate. */
  signIn(): void {
    void this.auth.login();
  }
}
