import { ChangeDetectionStrategy, Component, computed, inject, input, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { translateText } from 'core';

import { GenericPhotoComponent } from './generic-photo.component';
import type { ResultCardViewModel } from './result-card.view-model';

/**
 * One result card (dumb / presentational).
 *
 * Renders a single backend-ranked restaurant from its resolved {@link ResultCardViewModel} — pure
 * inputs/outputs, no store/data-access. It owns the per-card layout that honours the invariants:
 *
 * - **Basket price** is rendered as the pre-formatted string with **no `≈` marker and no per-card
 *   price disclaimer** (invariant #12 / §10 — the single footer/`ⓘ` notice carries that). Absent price
 *   renders nothing.
 * - **Rating** is our **smoothed** value + count with the discreet low-review note (invariant #6),
 *   never a Google rating. The note copy is resolved from the presenter's i18n key (inline `uk`
 *   fallback until Step 17, matching the shared error-state pattern).
 * - **Distance** shows only when present (geo was provided — invariant #11).
 * - **Coverage** ("N of M") is rendered as clearly **secondary** information (a muted caption), never
 *   as the ordering key (invariant #5).
 * - **Imagery** is the generic-by-default photo (invariant #8) via {@link GenericPhotoComponent}.
 *
 * The card is a semantic article with a labelled heading and a keyboard-focusable "open" affordance
 * (WCAG); opening emits `open` so the smart container can fire `card_open` and navigate later.
 */
@Component({
  selector: 'app-result-card',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule, MatCardModule, MatIconModule, TranslatePipe, GenericPhotoComponent],
  template: `
    <mat-card
      class="dp-card"
      appearance="outlined"
      [attr.data-testid]="'result-card-' + vm().restaurantId"
    >
      <article class="dp-card__body" [attr.aria-label]="vm().name">
        <div class="dp-card__media">
          <app-generic-photo [photo]="vm().photo" />
        </div>

        <div class="dp-card__content">
          <h3 class="dp-card__name" data-testid="result-name">{{ vm().name }}</h3>

          <div class="dp-card__metrics">
            @if (vm().basketPrice !== null) {
              <span class="dp-card__price" data-testid="result-price">{{ vm().basketPrice }}</span>
            }

            <span class="dp-card__rating" data-testid="result-rating">
              <span class="dp-sr-only">{{ ratingAria() }}</span>
              <mat-icon class="dp-card__rating-icon" aria-hidden="true">star</mat-icon>
              @if (vm().rating.hasRating) {
                <span class="dp-card__rating-value" aria-hidden="true">{{
                  vm().rating.value
                }}</span>
                <span class="dp-card__rating-count" aria-hidden="true"
                  >({{ vm().rating.count }})</span
                >
              } @else {
                <span class="dp-card__rating-value" aria-hidden="true">—</span>
              }
            </span>

            @if (vm().distance !== null) {
              <span class="dp-card__distance" data-testid="result-distance">
                <mat-icon class="dp-card__distance-icon" aria-hidden="true">near_me</mat-icon>
                {{ vm().distance }}
              </span>
            }
          </div>

          @if (ratingNote() !== null) {
            <p class="dp-card__rating-note" data-testid="result-rating-note">{{ ratingNote() }}</p>
          }

          @if (showCoverage()) {
            <p class="dp-card__coverage" data-testid="result-coverage">
              {{ coverageLabel() }}
            </p>
          }
        </div>

        <div class="dp-card__actions">
          <button
            mat-stroked-button
            type="button"
            class="dp-card__map"
            data-testid="result-view-map"
            [attr.aria-label]="viewMapAria()"
            (click)="viewMap.emit(vm())"
          >
            <mat-icon aria-hidden="true">map</mat-icon>
            {{ 'consumer.results.viewMap' | translate }}
          </button>

          <button
            mat-stroked-button
            type="button"
            class="dp-card__open"
            data-testid="result-open"
            [attr.aria-label]="openAria()"
            (click)="open.emit(vm())"
          >
            <mat-icon aria-hidden="true">chevron_right</mat-icon>
            {{ 'consumer.results.open' | translate }}
          </button>
        </div>
      </article>
    </mat-card>
  `,
  styles: `
    // Breakpoints are Sass vars (CSS custom props can't be read in @media).
    @use 'tokens';

    :host {
      display: block;
    }

    .dp-card {
      padding: var(--dp-space-3);
    }

    .dp-card__body {
      display: grid;
      grid-template-columns: var(--dp-card-thumb-size) 1fr auto;
      gap: var(--dp-space-4);
      align-items: center;
    }

    .dp-card__actions {
      display: flex;
      flex-direction: column;
      gap: var(--dp-space-2);
    }

    .dp-card__media {
      inline-size: var(--dp-card-thumb-size);
    }

    .dp-card__content {
      display: flex;
      flex-direction: column;
      gap: var(--dp-space-1);
      min-inline-size: 0;
    }

    .dp-card__name {
      margin: 0;
      font-size: var(--dp-font-size-subtitle);
      font-weight: var(--dp-font-weight-bold);
    }

    .dp-card__metrics {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: var(--dp-space-4);
      font-size: var(--dp-font-size-body);
    }

    .dp-card__price {
      font-weight: var(--dp-font-weight-medium);
    }

    .dp-card__rating,
    .dp-card__distance {
      display: inline-flex;
      align-items: center;
      gap: var(--dp-space-1);
    }

    .dp-card__rating-icon {
      color: var(--dp-color-warning);
    }

    .dp-card__rating-count,
    .dp-card__distance {
      color: var(--dp-color-on-surface-variant);
    }

    .dp-card__rating-note {
      margin: 0;
      font-size: var(--dp-font-size-caption);
      color: var(--dp-color-on-surface-variant);
    }

    /* Coverage is intentionally de-emphasised (secondary) — never the ordering signal (invariant #5). */
    .dp-card__coverage {
      margin: 0;
      font-size: var(--dp-font-size-caption);
      color: var(--dp-color-on-surface-variant);
    }

    @media (max-width: tokens.$dp-breakpoint-sm) {
      .dp-card__body {
        grid-template-columns: var(--dp-card-thumb-size-sm) 1fr;
        grid-template-areas:
          'media content'
          'open open';
      }

      .dp-card__media {
        grid-area: media;
        inline-size: var(--dp-card-thumb-size-sm);
      }

      .dp-card__content {
        grid-area: content;
      }

      .dp-card__actions {
        grid-area: open;
        flex-direction: row;
        flex-wrap: wrap;
        justify-content: flex-end;
      }
    }
  `,
})
export class ResultCardComponent {
  private readonly translate = inject(TranslateService);

  /** The resolved view-model for this backend-ranked restaurant. */
  readonly vm = input.required<ResultCardViewModel>();

  /** Emitted when the user opens the card (the container fires `card_open` and navigates later). */
  readonly open = output<ResultCardViewModel>();

  /**
   * Emitted when the user taps "Глянути на карті" (§5.7) — the container opens the inline map modal
   * without leaving the list. Separate from {@link open} so the map flow never navigates into details.
   */
  readonly viewMap = output<ResultCardViewModel>();

  /** Show the secondary coverage caption only when there is a selection to cover (total > 0). */
  readonly showCoverage = computed<boolean>(() => this.vm().coverageTotal > 0);

  /**
   * "N з M" coverage caption — secondary info (invariant #5). Uses ngx-translate's reactive
   * `translate()` signal API: the key params are an arrow function tracking `vm()`, and the returned
   * signal auto-updates on a language switch, so the caption re-resolves without manual plumbing.
   */
  readonly coverageLabel = translateText(this.translate, 'consumer.results.coverage', () => ({
    covered: this.vm().coverageCovered,
    total: this.vm().coverageTotal,
  }));

  /** The discreet rating note copy from the presenter's i18n key, or `null` when no note is needed. */
  private readonly ratingNoteSignal = translateText(
    this.translate,
    () => this.vm().rating.noteKey ?? 'rating.lowReviewNote',
    () => ({ count: this.vm().rating.count }),
  );
  readonly ratingNote = computed<string | null>(() =>
    this.vm().rating.noteKey === null ? null : this.ratingNoteSignal(),
  );

  private readonly openLabel = translateText(this.translate, 'consumer.results.open');
  private readonly viewMapLabel = translateText(this.translate, 'consumer.results.viewMap');

  readonly openAria = translateText(this.translate, 'consumer.results.openAria', () => ({
    action: this.openLabel(),
    name: this.vm().name,
  }));

  readonly viewMapAria = translateText(this.translate, 'consumer.results.viewMapAria', () => ({
    action: this.viewMapLabel(),
    name: this.vm().name,
  }));

  /**
   * Consolidated screen-reader label for the rating block. The visual star icon + value + count are
   * `aria-hidden`, so this single phrase ("Рейтинг 4.5 із 5 за 12 відгуками" / "Рейтингу ще немає")
   * is what assistive tech announces — avoiding a disjoint "star 4.5 12" read-out (WCAG 1.1.1).
   */
  private readonly ratingValueAria = translateText(
    this.translate,
    'consumer.results.ratingAria',
    () => ({ value: this.vm().rating.value, count: this.vm().rating.count }),
  );
  private readonly noRatingAria = translateText(this.translate, 'consumer.results.noRatingAria');
  readonly ratingAria = computed<string>(() =>
    this.vm().rating.hasRating ? this.ratingValueAria() : this.noRatingAria(),
  );
}
