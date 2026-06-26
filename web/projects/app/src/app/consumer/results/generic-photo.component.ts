import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

import type { ResultCardPhoto } from './result-card.view-model';

/**
 * Generic / real card imagery (dumb / presentational).
 *
 * Generic category/dish images are **our own content** and the default on every card (invariant #8) —
 * this component renders the generic asset unless the descriptor says the backend permitted a real
 * venue photo, in which case it shows the real photo instead. It never renders a third-party photo by
 * default.
 *
 * Performance (design §Performance): the image is **lazy-loaded** (`loading="lazy"`), gives the
 * browser sizing hints (`decoding="async"`) and is styled responsive (fluid width, fixed aspect ratio)
 * so long lists do not pay for off-screen decode. A generic asset is purely decorative for the card
 * (the name/price carry the meaning), so it is `aria-hidden` with an empty `alt`; a real photo keeps
 * its descriptive `alt` for screen readers.
 */
@Component({
  selector: 'app-generic-photo',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <img
      class="dp-photo__img"
      [src]="photo().src"
      [alt]="altText()"
      [attr.aria-hidden]="isGeneric() ? 'true' : null"
      [attr.data-testid]="isGeneric() ? 'generic-photo' : 'real-photo'"
      loading="lazy"
      decoding="async"
    />
  `,
  styles: `
    :host {
      display: block;
    }

    .dp-photo__img {
      display: block;
      inline-size: 100%;
      block-size: 100%;
      object-fit: cover;
      aspect-ratio: 16 / 9;
      border-radius: var(--dp-radius-sm);
      background-color: var(--dp-color-surface-variant);
    }
  `,
})
export class GenericPhotoComponent {
  /** The photo descriptor (generic by default; real only where the backend permitted it). */
  readonly photo = input.required<ResultCardPhoto>();

  /** True when the rendered image is our generic asset (the default — invariant #8). */
  readonly isGeneric = computed<boolean>(() => !this.photo().isReal);

  /** Generic assets are decorative (empty alt); real photos keep their descriptive alt. */
  readonly altText = computed<string>(() => (this.isGeneric() ? '' : this.photo().alt));
}
