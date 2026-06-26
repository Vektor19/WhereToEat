import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { TranslatePipe } from '@ngx-translate/core';

import { ResultCardComponent } from './result-card.component';
import type { ResultCardViewModel } from './result-card.view-model';

/**
 * Labeled, visually-separated ad slot (dumb / presentational) — the **invariant #10 presentation
 * contract**.
 *
 * When a sponsored result is present, it is rendered through this slot rather than inline with the
 * organic cards, so it is **clearly labeled "Реклама / Промо" and visually separated** from the
 * objective organic ranking (Закон «Про рекламу»; the organic rank is never sold). It wraps the same
 * {@link ResultCardComponent} so a sponsored venue looks consistent, but adds the partner label and a
 * distinct, bordered container that sets it apart from the organic list.
 *
 * **No fake ad data in the real flow.** The current backend `recommend` response has no ad-slot field,
 * so the container only renders this slot when a view-model with `isAd === true` is supplied — i.e. it
 * is a contract the list is *structured* to honour when the backend adds it, never invented data.
 */
@Component({
  selector: 'app-ad-slot',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatCardModule, MatIconModule, TranslatePipe, ResultCardComponent],
  template: `
    <section
      class="dp-ad"
      [attr.aria-label]="'consumer.results.adLabel' | translate"
      data-testid="ad-slot"
      [attr.data-ad-restaurant]="vm().restaurantId"
    >
      <div class="dp-ad__label" data-testid="ad-label">
        <mat-icon class="dp-ad__label-icon" aria-hidden="true">campaign</mat-icon>
        <span>{{ 'consumer.results.adLabel' | translate }}</span>
      </div>

      <app-result-card [vm]="vm()" (open)="open.emit($event)" (viewMap)="viewMap.emit($event)" />
    </section>
  `,
  styles: `
    :host {
      display: block;
    }

    /* Visually separated from the organic list: distinct border + tint, with a clear paid label. */
    .dp-ad {
      display: flex;
      flex-direction: column;
      gap: var(--dp-space-2);
      padding: var(--dp-space-3);
      border: var(--dp-border-width-hairline) solid var(--dp-color-accent);
      border-radius: var(--dp-radius-md);
      background-color: var(--dp-color-accent-container);
    }

    .dp-ad__label {
      display: inline-flex;
      align-items: center;
      gap: var(--dp-space-1);
      align-self: flex-start;
      font-size: var(--dp-font-size-caption);
      font-weight: var(--dp-font-weight-bold);
      text-transform: uppercase;
      letter-spacing: 0.04em;
      color: var(--dp-color-on-accent-container);
    }

    .dp-ad__label-icon {
      font-size: var(--dp-font-size-body);
      inline-size: var(--dp-font-size-body);
      block-size: var(--dp-font-size-body);
    }
  `,
})
export class AdSlotComponent {
  /** The sponsored restaurant's view-model (`isAd === true`). */
  readonly vm = input.required<ResultCardViewModel>();

  /** Re-emitted card-open so the container handles a sponsored open identically. */
  readonly open = output<ResultCardViewModel>();

  /** Re-emitted "view on map" so a sponsored card opens the inline map modal identically (§5.7). */
  readonly viewMap = output<ResultCardViewModel>();
}
