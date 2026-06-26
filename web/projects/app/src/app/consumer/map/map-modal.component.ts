import { A11yModule } from '@angular/cdk/a11y';
import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  afterNextRender,
  computed,
  effect,
  inject,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { DomSanitizer, type SafeResourceUrl } from '@angular/platform-browser';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';

import {
  AnalyticsEmitterService,
  AnalyticsEventBuilders,
  GET_MAP,
  LoadingService,
  MAP_PROVIDER,
  translateText,
  type MapPayloadDto,
  type MapView,
} from 'core';

import { LoadingComponent } from '../../shared/ui/loading.component';
import { ErrorStateComponent } from '../../shared/ui/error-state.component';

/**
 * Inline **"Глянути на карті / View on map"** modal (smart) — §5.7, design decision #11.
 *
 * Opened from a result card (the user does **not** navigate into details), it fetches the live
 * `GET /map/{restaurantId}` payload via {@link GET_MAP} and resolves it through the active, swappable
 * {@link MAP_PROVIDER} port into a view-ready {@link MapView}: a live **Embed** iframe, a **deep-link**
 * affordance (the degradation when no Embed credential), or the **no-map** empty state
 * (`hasMapData === false`). §5.7 / invariants #6 & #7 hold — the view is **live**, **nothing is
 * cached**, and **no Google rating / coordinate is stored**; the Google rating shows only inside the
 * live Embed.
 *
 * **Accessibility (WCAG).** It is an ARIA `dialog` (`aria-modal`) with a labelled title, a CDK
 * focus-trap (`cdkTrapFocus`), **ESC closes**, the backdrop closes, and focus is **returned** to the
 * invoking element on close. On open the **dialog title** is focused (it carries `tabindex="-1"`) so
 * screen readers announce the dialog, then the trap keeps focus inside. On open it emits one `action`
 * analytics event through the single {@link AnalyticsEmitterService} batching path (best-effort,
 * non-blocking).
 *
 * **iframe safety.** The Embed iframe is sanitised via {@link DomSanitizer.bypassSecurityTrustResourceUrl}
 * — safe **only** because the URL is entirely app-constructed (a config-keyed Google Maps Embed URL built
 * from an environment credential plus backend-supplied fields), **never** user input — and given
 * `referrerpolicy="no-referrer"` plus an explicit `sandbox`.
 *
 * ⚠️ The sandbox value `allow-scripts allow-same-origin` is a **known sandbox-escape anti-pattern**: a
 * same-origin framed script can reach up and remove its own `sandbox` attribute, neutralising the
 * sandbox. We accept it here **only** because the Google Maps Embed API genuinely requires *both* flags
 * (scripts to render the map, same-origin so its tiles/API calls work) **and** the framed origin is a
 * trusted, app-built Google URL — never an untrusted or user-supplied origin. **Do NOT copy this flag
 * combination** for any iframe whose `src` could be untrusted or attacker-influenced; for those, drop
 * `allow-same-origin` (or sandbox to an opaque origin).
 */
@Component({
  selector: 'app-map-modal',
  changeDetection: ChangeDetectionStrategy.OnPush,
  // ESC closes the dialog from anywhere inside it (WCAG 2.1.2 / dialog pattern): the host listens for
  // Escape so the keyboard path does not depend on the non-focusable presentational backdrop.
  host: { '(keydown.escape)': 'requestClose()' },
  imports: [
    A11yModule,
    MatButtonModule,
    MatIconModule,
    LoadingComponent,
    ErrorStateComponent,
    TranslatePipe,
  ],
  template: `
    <div
      class="dp-map-backdrop"
      role="presentation"
      data-testid="map-backdrop"
      (click)="onBackdrop($event)"
    >
      <section
        class="dp-map-dialog"
        role="dialog"
        aria-modal="true"
        [attr.aria-label]="title()"
        data-testid="map-dialog"
        cdkTrapFocus
      >
        <header class="dp-map-dialog__header">
          <h2 #dialogTitle class="dp-map-dialog__title" tabindex="-1">{{ title() }}</h2>
          <button
            mat-icon-button
            type="button"
            class="dp-map-dialog__close"
            [attr.aria-label]="'consumer.map.close' | translate"
            data-testid="map-close"
            (click)="requestClose()"
          >
            <mat-icon aria-hidden="true">close</mat-icon>
          </button>
        </header>

        <div class="dp-map-dialog__body">
          @if (loading()) {
            <app-loading [label]="'consumer.map.loading' | translate" />
          } @else if (error() !== undefined) {
            <app-error-state [error]="error()" [retryable]="true" (retry)="reload()" />
          } @else if (view()?.kind === 'embed') {
            <iframe
              class="dp-map-dialog__embed"
              data-testid="map-embed"
              [src]="embedSrc()"
              [title]="embedTitle()"
              loading="lazy"
              referrerpolicy="no-referrer"
              sandbox="allow-scripts allow-same-origin"
            ></iframe>
          } @else if (view()?.kind === 'deep-link') {
            <div class="dp-map-dialog__deeplink" data-testid="map-deeplink">
              <mat-icon class="dp-map-dialog__deeplink-icon" aria-hidden="true">map</mat-icon>
              <p class="dp-map-dialog__deeplink-text">
                {{ 'consumer.map.deepLinkMessage' | translate }}
              </p>
              <a
                mat-stroked-button
                data-testid="map-deeplink-link"
                [href]="deepLinkHref()"
                target="_blank"
                rel="noopener noreferrer"
              >
                <mat-icon aria-hidden="true">open_in_new</mat-icon>
                {{ 'consumer.map.deepLinkLabel' | translate }}
              </a>
            </div>
          } @else {
            <app-error-state
              [emptyMessage]="'consumer.map.noData' | translate"
              [retryable]="false"
            />
          }
        </div>
      </section>
    </div>
  `,
  styles: `
    .dp-map-backdrop {
      position: fixed;
      inset: 0;
      z-index: var(--dp-z-modal, 1000);
      display: flex;
      align-items: center;
      justify-content: center;
      padding: var(--dp-space-4);
      background-color: var(--dp-color-scrim);
    }

    .dp-map-dialog {
      display: flex;
      flex-direction: column;
      inline-size: min(100%, 40rem);
      max-block-size: min(90vh, 40rem);
      overflow: hidden;
      background-color: var(--dp-color-surface);
      border-radius: var(--dp-radius-lg);
      box-shadow: var(--dp-elevation-3);
    }

    .dp-map-dialog__header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: var(--dp-space-3);
      padding: var(--dp-space-3) var(--dp-space-4);
      border-block-end: var(--dp-border-width-hairline) solid var(--dp-color-outline-variant);
    }

    .dp-map-dialog__title {
      margin: 0;
      font-size: var(--dp-font-size-subtitle);
      font-weight: var(--dp-font-weight-bold);
      outline: none;
    }

    .dp-map-dialog__body {
      flex: 1 1 auto;
      min-block-size: 18rem;
      overflow: auto;
    }

    .dp-map-dialog__embed {
      display: block;
      inline-size: 100%;
      block-size: 24rem;
      border: 0;
    }

    .dp-map-dialog__deeplink {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: var(--dp-space-3);
      padding: var(--dp-space-6) var(--dp-space-4);
      text-align: center;
      color: var(--dp-color-on-surface-variant);
    }

    .dp-map-dialog__deeplink-text {
      margin: 0;
      font-size: var(--dp-font-size-body);
    }
  `,
})
export class MapModalComponent {
  private readonly getMap = inject(GET_MAP);
  private readonly provider = inject(MAP_PROVIDER);
  private readonly events = inject(AnalyticsEventBuilders);
  private readonly emitter = inject(AnalyticsEmitterService);
  private readonly loadingService = inject(LoadingService);
  private readonly sanitizer = inject(DomSanitizer);
  private readonly translate = inject(TranslateService);

  /** The restaurant whose live map to show. */
  readonly restaurantId = input.required<string>();
  /** The restaurant display name (the dialog title / iframe label, for accessibility). */
  readonly name = input<string>('');

  /** Emitted when the user dismisses the modal (ESC, the close button, or the backdrop). */
  readonly closed = output<void>();

  /** The fetched live map payload's async state (loading / error / loaded), via the shared primitive. */
  private readonly state = this.loadingService.create<MapPayloadDto>();

  /** The title element, focused on open so the dialog announces itself to screen readers. */
  private readonly dialogTitle = viewChild<ElementRef<HTMLElement>>('dialogTitle');

  // ── Bound async state ────────────────────────────────────────────────────────
  readonly loading = this.state.loading;
  readonly error = this.state.error;

  /** The active provider's resolved view for the loaded payload, or `null` until it lands. */
  readonly view = computed<MapView | null>(() => {
    const payload = this.state.data();
    return payload === undefined ? null : this.provider.resolve(payload);
  });

  /** The localized "on map" title prefix, reactive so a language switch re-resolves it. */
  private readonly mapTitlePrefix = translateText(this.translate, 'consumer.map.titlePrefix');

  readonly title = computed<string>(() => {
    const name = this.name();
    const prefix = this.mapTitlePrefix();
    return name.length > 0 ? `${prefix} ${name}` : prefix;
  });

  /** The sandboxed, sanitised iframe `src` for the Embed view (an app-built, config-keyed URL). */
  readonly embedSrc = computed<SafeResourceUrl | null>(() => {
    const view = this.view();
    return view?.kind === 'embed' ? this.sanitizer.bypassSecurityTrustResourceUrl(view.url) : null;
  });

  /** The deep-link `href` for the degraded view, or `null`. */
  readonly deepLinkHref = computed<string | null>(() => {
    const view = this.view();
    return view?.kind === 'deep-link' ? view.url : null;
  });

  readonly embedTitle = computed<string>(() => this.title());

  /** Marks whether the `action` event has fired for the current open, so reload does not re-emit it. */
  private readonly actionEmitted = signal(false);

  constructor() {
    // (Re)fetch whenever the target restaurant changes, and emit the `action` event once per open.
    effect(() => {
      const id = this.restaurantId();
      this.fetch(id);
      if (!this.actionEmitted()) {
        this.actionEmitted.set(true);
        this.emitter.emit(this.events.action({ restaurantId: id }));
      }
    });

    // Focus the dialog title once the modal has rendered so screen readers announce the dialog; the
    // CDK focus-trap then keeps focus inside (we drive initial focus ourselves instead of auto-capture).
    afterNextRender(() => this.dialogTitle()?.nativeElement?.focus());
  }

  /** Re-run the map fetch (the shared error-state retry affordance). */
  reload(): void {
    this.fetch(this.restaurantId());
  }

  /** Close on a backdrop click (only when the backdrop itself — not the dialog — was clicked). */
  onBackdrop(event: MouseEvent): void {
    if (event.target === event.currentTarget) {
      this.requestClose();
    }
  }

  /** Request dismissal; the parent removes the modal, which returns focus to the invoking element. */
  requestClose(): void {
    this.closed.emit();
  }

  // ── Internals ───────────────────────────────────────────────────────────────

  private fetch(restaurantId: string): void {
    this.state.run(this.getMap.execute(restaurantId));
  }
}
