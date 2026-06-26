import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';

import { GeoOptInStore, GeolocationService, SelectionStore, translateText } from 'core';

/**
 * Geo opt-in control (smart) — the opt-in approximate geolocation + distance-ranking toggle
 * (invariant #11). Distance ranking is **off by default** and only turns on when the user explicitly
 * opts in; turning it off ranges further for price/quality.
 *
 * It is the only place that touches the geolocation seam and the geo state:
 *   - on toggle **on** it calls the {@link GeolocationService} (which triggers the browser permission
 *     prompt and returns **approximate** coordinates), and on success feeds them into the
 *     {@link SelectionStore} via `setGeo` (so the request builder includes `userGeo` and the results
 *     show km) and records the persisted opt-in via {@link GeoOptInStore}; on a handled
 *     denial/unavailable/timeout it reverts the toggle and shows a discreet i18n-keyed message (no
 *     crash);
 *   - on toggle **off** it calls `clearGeo` (so `userGeo` is **omitted** from the wire body — invariant
 *     #11 — and distance is hidden) and records the opt-out.
 *
 * The opt-in choice persists across sessions (in {@link GeoOptInStore}); the coordinates are **not**
 * persisted — when the persisted choice is on, this control silently re-acquires an approximate
 * position on init (the browser will not re-prompt while permission is still granted, and it reverts to
 * off if permission was meanwhile revoked), so a stale position is never reused across sessions.
 *
 * The consent copy is discreet and honest (approximate, optional, not sold — invariant #11 / §10's
 * minimal-disclaimer stance). The toggle is an accessible Material slide-toggle (label + ARIA + keyboard
 * for free). All copy — including each handled-failure message — resolves from the runtime i18n
 * catalogs (Step 17); a failure is carried as its `consumer.geo.error.*` key and localized for display.
 */
@Component({
  selector: 'app-geo-optin',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatSlideToggleModule, MatIconModule, MatProgressSpinnerModule, TranslatePipe],
  template: `
    <section class="dp-geo" data-testid="geo-optin">
      <div class="dp-geo__row">
        <mat-slide-toggle
          class="dp-geo__toggle"
          data-testid="geo-toggle"
          [checked]="optedIn()"
          [disabled]="acquiring()"
          (change)="onToggle($event.checked)"
        >
          {{ 'consumer.geo.toggle' | translate }}
        </mat-slide-toggle>

        @if (acquiring()) {
          <mat-progress-spinner
            class="dp-geo__spinner"
            mode="indeterminate"
            diameter="18"
            [attr.aria-label]="'consumer.geo.acquiring' | translate"
            data-testid="geo-acquiring"
          />
        }
      </div>

      <p class="dp-geo__consent" data-testid="geo-consent">
        <mat-icon class="dp-geo__consent-icon" aria-hidden="true">info</mat-icon>
        <span>{{ 'consumer.geo.consent' | translate }}</span>
      </p>

      @if (errorMessageKey(); as key) {
        <p class="dp-geo__error" role="alert" data-testid="geo-error" [attr.data-message-key]="key">
          {{ errorText() }}
        </p>
      }
    </section>
  `,
  styles: `
    .dp-geo {
      display: flex;
      flex-direction: column;
      gap: var(--dp-space-2);
    }

    .dp-geo__row {
      display: flex;
      align-items: center;
      gap: var(--dp-space-3);
    }

    .dp-geo__consent {
      display: flex;
      align-items: flex-start;
      gap: var(--dp-space-2);
      margin: 0;
      color: var(--dp-color-on-surface-variant);
      font-size: var(--dp-font-size-caption);
    }

    .dp-geo__consent-icon {
      flex: none;
      font-size: var(--dp-font-size-body);
      width: var(--dp-font-size-body);
      height: var(--dp-font-size-body);
    }

    .dp-geo__error {
      margin: 0;
      color: var(--dp-color-error);
      font-size: var(--dp-font-size-caption);
    }
  `,
})
export class GeoOptInComponent {
  private readonly geolocation = inject(GeolocationService);
  private readonly optInStore = inject(GeoOptInStore);
  private readonly selection = inject(SelectionStore);
  private readonly translate = inject(TranslateService);

  /** True while a coordinate acquisition is in flight (toggle is disabled meanwhile). */
  readonly acquiring = signal<boolean>(false);
  /** The i18n message key of the last handled failure (e.g. `consumer.geo.error.timeout`), or `null`. */
  readonly errorMessageKey = signal<string | null>(null);

  /** True when the user is currently opted in (persisted choice). */
  readonly optedIn = this.optInStore.optedIn;

  /**
   * Resolve the current error key to localized copy through the runtime catalogs (Step 17). The key
   * arrow tracks `errorMessageKey()` so the message re-resolves on a switch; an empty key arrow yields
   * the empty string so no error row renders when there is no failure.
   */
  private readonly resolvedError = translateText(
    this.translate,
    () => this.errorMessageKey() ?? 'common.empty',
  );

  /** The localized failure copy, or the empty string when there is no handled failure. */
  readonly errorText = computed<string>(() =>
    this.errorMessageKey() === null ? '' : this.resolvedError(),
  );

  constructor() {
    // Re-honour a persisted opt-in by silently re-acquiring (no re-prompt while permission stands);
    // reverts to off if permission was revoked between sessions. Coordinates are never persisted.
    if (this.optInStore.optedIn() && this.selection.userGeo() === undefined) {
      void this.acquireAndApply(true);
    }
  }

  /** Handle the toggle: opt in (acquire + set geo) or opt out (clear geo), persisting the choice. */
  onToggle(checked: boolean): void {
    this.errorMessageKey.set(null);
    if (checked) {
      void this.acquireAndApply(false);
    } else {
      this.optOut();
    }
  }

  /**
   * Acquire approximate coordinates and, on success, set `userGeo` + persist the opt-in. On a handled
   * failure, surface the message key and (for an explicit toggle) revert to off so the UI never claims
   * an opt-in that did not happen. `silent` is the init re-acquire path — it reverts the persisted
   * choice without a re-prompt loop and shows no error toast.
   */
  private async acquireAndApply(silent: boolean): Promise<void> {
    this.acquiring.set(true);
    try {
      const result = await this.geolocation.acquire();
      if (result.ok) {
        this.selection.setGeo(result.coordinate);
        this.optInStore.optIn();
        return;
      }
      // Handled failure: never opt in on a failed acquisition.
      this.optOut();
      if (!silent) {
        this.errorMessageKey.set(result.messageKey);
      }
    } finally {
      this.acquiring.set(false);
    }
  }

  /** Drop the approximate geo (omit `userGeo` — invariant #11) and persist the opt-out. */
  private optOut(): void {
    this.selection.clearGeo();
    this.optInStore.optOut();
  }
}
