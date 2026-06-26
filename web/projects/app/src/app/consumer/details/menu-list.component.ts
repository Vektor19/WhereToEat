import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

import { GenericPhotoComponent } from '../results/generic-photo.component';
import type { MenuItemViewModel } from './details.view-model';

/**
 * Restaurant menu list (dumb / presentational).
 *
 * Renders the resolved {@link MenuItemViewModel}s in backend order — pure strings only, no store/
 * data-access. Each row shows the **generic-by-default photo** (invariant #8) via the shared
 * {@link GenericPhotoComponent}, the dish name, the **pre-formatted price** (formatted by the item's
 * `priceCurrency` in the container — never a hardcoded symbol, invariant §3), and the **weight only
 * when present**.
 *
 * It carries **no per-item price/photo disclaimer and no "updated X days ago"** (invariant #12 / §10)
 * — the single discreet footer/`ⓘ` notice (Step 2) is the sole data-accuracy notice.
 */
@Component({
  selector: 'app-menu-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe, GenericPhotoComponent],
  template: `
    <section [attr.aria-label]="'consumer.details.menuRegion' | translate" data-testid="menu-list">
      <h2 class="dp-menu__title">{{ 'consumer.details.menuTitle' | translate }}</h2>

      @if (items().length > 0) {
        <ul class="dp-menu__list">
          @for (item of items(); track item.id) {
            <li class="dp-menu__item" [attr.data-testid]="'menu-item-' + item.id">
              <div class="dp-menu__media">
                <app-generic-photo [photo]="item.photo" />
              </div>

              <div class="dp-menu__content">
                <span class="dp-menu__name" data-testid="menu-item-name">{{ item.dishName }}</span>
                @if (item.weight !== null) {
                  <span class="dp-menu__weight" data-testid="menu-item-weight">{{
                    item.weight
                  }}</span>
                }
              </div>

              <span class="dp-menu__price" data-testid="menu-item-price">{{ item.price }}</span>
            </li>
          }
        </ul>
      } @else {
        <p class="dp-menu__empty" data-testid="menu-list-empty">
          {{ 'consumer.details.menuEmpty' | translate }}
        </p>
      }
    </section>
  `,
  styles: `
    :host {
      display: block;
    }

    .dp-menu__title {
      margin: 0 0 var(--dp-space-3);
      font-size: var(--dp-font-size-subtitle);
      font-weight: var(--dp-font-weight-bold);
    }

    .dp-menu__list {
      display: flex;
      flex-direction: column;
      gap: var(--dp-space-3);
      margin: 0;
      padding: 0;
      list-style: none;
    }

    .dp-menu__item {
      display: grid;
      grid-template-columns: var(--dp-card-thumb-size) 1fr auto;
      gap: var(--dp-space-4);
      align-items: center;
      padding-block-end: var(--dp-space-3);
      border-block-end: var(--dp-border-width-hairline) solid var(--dp-color-outline-variant);
    }

    .dp-menu__media {
      inline-size: var(--dp-card-thumb-size);
    }

    .dp-menu__content {
      display: flex;
      flex-direction: column;
      gap: var(--dp-space-1);
      min-inline-size: 0;
    }

    .dp-menu__name {
      font-weight: var(--dp-font-weight-medium);
    }

    .dp-menu__weight {
      font-size: var(--dp-font-size-caption);
      color: var(--dp-color-on-surface-variant);
    }

    .dp-menu__price {
      font-weight: var(--dp-font-weight-medium);
      white-space: nowrap;
    }

    .dp-menu__empty {
      margin: 0;
      font-size: var(--dp-font-size-body);
      color: var(--dp-color-on-surface-variant);
    }
  `,
})
export class MenuListComponent {
  /** The resolved menu items in backend order. */
  readonly items = input.required<readonly MenuItemViewModel[]>();
}
