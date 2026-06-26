/**
 * Smoothed-rating presenter (Step 8, invariant #6). The backend already computes the **smoothed,
 * cumulative all-time** rating (Bayesian additive prior; the recommend/details DTOs carry the
 * `smoothedRating` + `ratingCount`). This service only **presents** it: a localised numeric value,
 * the review count, and — critically — a discreet **low-review-count note** when the count is below
 * a confidence threshold, so the UI is honest that a low-sample average is less certain (invariant
 * #6: "пригладжуємо до нейтрального доти, доки відгуків не набереться достатньо"). It also models
 * the "no reviews yet" case so the UI never shows a misleading bare number.
 *
 * It never produces or references a Google rating — our ratings are our own (invariant #6); the
 * Google rating is visible only inside the live map Embed (§5.7) and is never presented here.
 *
 * **Locale-ready without baking copy:** the low-review note is exposed as an i18n **message key**
 * (`noteKey`), not a hardcoded UA/EN string, so Step 17 resolves the localized copy with the review
 * count as an interpolation argument. The numeric value is locale-aware via `Intl`. This keeps the
 * presenter free of launch-locale copy that would block Step 17.
 *
 * Framework-light: an injectable pure presenter mirrored 1:1 by the RN rewrite.
 */
import { Injectable } from '@angular/core';

import { DEFAULT_LOCALE } from './currency.formatter';

/**
 * The review-count threshold below which the smoothed average is presented as low-confidence (a
 * discreet note is attached). Aligned with the backend's default Bayesian prior weight `C = 10`
 * (`BayesianRatingSmoothing.DefaultPriorWeight`): below the prior weight the neutral mean still
 * dominates the raw average, so the value is explicitly low-confidence. Inclusive — a count *equal*
 * to the threshold no longer shows the note.
 */
export const LOW_REVIEW_THRESHOLD = 10;

/** The i18n message key for the discreet low-review-count note (resolved by Step 17 with `count`). */
export const LOW_REVIEW_NOTE_KEY = 'rating.lowReviewNote';

/** The i18n message key for the "no reviews yet" state (resolved by Step 17). */
export const NO_REVIEWS_KEY = 'rating.noReviews';

/**
 * The view-model the rating presenter produces. It is fully resolved for rendering **except** the
 * note copy, which is a stable {@link LOW_REVIEW_NOTE_KEY}/{@link NO_REVIEWS_KEY} message key (Step
 * 17 resolves it) — so the presenter carries no launch-locale string.
 */
export interface RatingPresentation {
  /** True when the venue has at least one review (a smoothed value worth showing exists). */
  readonly hasRating: boolean;
  /** The localised smoothed value (e.g. `4,3` in `uk`), or `null` when there are no reviews. */
  readonly value: string | null;
  /** The raw review count (for the "(N)" suffix and as the note's interpolation argument). */
  readonly count: number;
  /**
   * True when the count is below {@link LOW_REVIEW_THRESHOLD} — the UI shows the discreet
   * low-confidence note. Always paired with {@link noteKey}.
   */
  readonly isLowConfidence: boolean;
  /**
   * The i18n message key for the note to show ({@link LOW_REVIEW_NOTE_KEY} when low-confidence with
   * some reviews, {@link NO_REVIEWS_KEY} when there are none), or `null` when no note is needed
   * (enough reviews for confidence). The copy is resolved by Step 17, never baked here.
   */
  readonly noteKey: typeof LOW_REVIEW_NOTE_KEY | typeof NO_REVIEWS_KEY | null;
}

@Injectable({ providedIn: 'root' })
export class RatingPresenter {
  /**
   * Build the {@link RatingPresentation} from the DTO's `smoothedRating` (already smoothed by the
   * backend) and `ratingCount`. Both are optional/nullable on the wire (the engine omits the value
   * for a venue with no reviews) — this method handles every case:
   *   - no reviews (`count <= 0` or missing value) → no value, the {@link NO_REVIEWS_KEY} note;
   *   - some reviews below the threshold → the value + the discreet {@link LOW_REVIEW_NOTE_KEY} note;
   *   - enough reviews → the value + count, no note.
   */
  present(
    smoothedRating: number | null | undefined,
    ratingCount: number | null | undefined,
    locale: string = DEFAULT_LOCALE,
  ): RatingPresentation {
    const count = this.normaliseCount(ratingCount);

    if (count <= 0 || smoothedRating === null || smoothedRating === undefined) {
      return {
        hasRating: false,
        value: null,
        count,
        isLowConfidence: true,
        noteKey: NO_REVIEWS_KEY,
      };
    }

    const value = this.formatValue(smoothedRating, locale);
    const isLowConfidence = count < LOW_REVIEW_THRESHOLD;

    return {
      hasRating: true,
      value,
      count,
      isLowConfidence,
      noteKey: isLowConfidence ? LOW_REVIEW_NOTE_KEY : null,
    };
  }

  private formatValue(smoothedRating: number, locale: string): string {
    if (!Number.isFinite(smoothedRating)) {
      return '';
    }
    return new Intl.NumberFormat(locale, {
      minimumFractionDigits: 1,
      maximumFractionDigits: 1,
    }).format(smoothedRating);
  }

  private normaliseCount(ratingCount: number | null | undefined): number {
    if (ratingCount === null || ratingCount === undefined || !Number.isFinite(ratingCount)) {
      return 0;
    }
    return Math.max(0, Math.trunc(ratingCount));
  }
}
