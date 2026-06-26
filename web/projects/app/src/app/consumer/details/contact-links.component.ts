import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { TranslatePipe } from '@ngx-translate/core';

import type { ContactLinkViewModel } from './details.view-model';

/**
 * Always-free contact links (dumb / presentational) — §5.8 / invariant #10.
 *
 * Renders the venue's site/social/phone links for **every** restaurant, Verified or not, with **no
 * paywall and no gating** (contact links are never monetized). Each link is a **safe external link**:
 * web links open in a new tab with `rel="noopener noreferrer"` and `target="_blank"`; a `tel:` link
 * dials in place (no new tab). The list is a semantic `nav` with an accessible label and one
 * descriptive label per link.
 *
 * It emits {@link linkClick} so the smart container can fire the `action` analytics event on a
 * contact-link click (§5.9) — the component itself stays free of analytics/store coupling.
 *
 * When the venue carries no contact links the component renders a discreet "none" note rather than an
 * empty region, so the always-present contract is visible even for a venue with an empty list.
 */
@Component({
  selector: 'app-contact-links',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatIconModule, TranslatePipe],
  template: `
    <nav
      class="dp-contacts"
      [attr.aria-label]="'consumer.details.contactsRegion' | translate"
      data-testid="contact-links"
    >
      <h2 class="dp-contacts__title">{{ 'consumer.details.contactsTitle' | translate }}</h2>

      @if (links().length > 0) {
        <ul class="dp-contacts__list">
          @for (link of links(); track link.url) {
            <li class="dp-contacts__item">
              @if (link.isPhone) {
                <a
                  class="dp-contacts__link"
                  data-testid="contact-link"
                  [href]="link.url"
                  [attr.data-kind]="link.kind"
                  [attr.aria-label]="ariaFor(link)"
                  (click)="linkClick.emit(link)"
                >
                  <mat-icon class="dp-contacts__icon" aria-hidden="true">{{
                    iconFor(link)
                  }}</mat-icon>
                  <span class="dp-contacts__text">{{ link.text }}</span>
                </a>
              } @else {
                <a
                  class="dp-contacts__link"
                  data-testid="contact-link"
                  [href]="link.url"
                  [attr.data-kind]="link.kind"
                  [attr.aria-label]="ariaFor(link)"
                  target="_blank"
                  rel="noopener noreferrer"
                  (click)="linkClick.emit(link)"
                >
                  <mat-icon class="dp-contacts__icon" aria-hidden="true">{{
                    iconFor(link)
                  }}</mat-icon>
                  <span class="dp-contacts__text">{{ link.text }}</span>
                </a>
              }
            </li>
          }
        </ul>
      } @else {
        <p class="dp-contacts__empty" data-testid="contact-links-empty">
          {{ 'consumer.details.contactsEmpty' | translate }}
        </p>
      }
    </nav>
  `,
  styles: `
    :host {
      display: block;
    }

    .dp-contacts__title {
      margin: 0 0 var(--dp-space-2);
      font-size: var(--dp-font-size-subtitle);
      font-weight: var(--dp-font-weight-bold);
    }

    .dp-contacts__list {
      display: flex;
      flex-wrap: wrap;
      gap: var(--dp-space-2);
      margin: 0;
      padding: 0;
      list-style: none;
    }

    .dp-contacts__link {
      display: inline-flex;
      align-items: center;
      gap: var(--dp-space-1);
      padding: var(--dp-space-1) var(--dp-space-3);
      border: var(--dp-border-width-hairline) solid var(--dp-color-outline-variant);
      border-radius: var(--dp-radius-pill, var(--dp-radius-sm));
      color: var(--dp-color-primary);
      text-decoration: none;
    }

    .dp-contacts__link:hover,
    .dp-contacts__link:focus-visible {
      background-color: var(--dp-color-surface-variant);
    }

    .dp-contacts__empty {
      margin: 0;
      font-size: var(--dp-font-size-caption);
      color: var(--dp-color-on-surface-variant);
    }
  `,
})
export class ContactLinksComponent {
  /** The always-shown contact links for this venue (possibly empty — §5.8 / invariant #10). */
  readonly links = input.required<readonly ContactLinkViewModel[]>();

  /** Emitted on a contact-link click — the container fires the `action` analytics event (§5.9). */
  readonly linkClick = output<ContactLinkViewModel>();

  /** A Material icon name for a contact kind (defaults to a generic link glyph for unknown kinds). */
  iconFor(link: ContactLinkViewModel): string {
    return CONTACT_ICONS[link.kind.toLowerCase()] ?? 'link';
  }

  /** The accessible label for a link (its visible text is also descriptive, but make it explicit). */
  ariaFor(link: ContactLinkViewModel): string {
    return link.text;
  }
}

/** Material icon names per known contact kind; unknown kinds fall back to a generic `link` glyph. */
const CONTACT_ICONS: Readonly<Record<string, string>> = {
  site: 'public',
  website: 'public',
  web: 'public',
  phone: 'call',
  tel: 'call',
  facebook: 'thumb_up',
  instagram: 'photo_camera',
  telegram: 'send',
  email: 'mail',
};
