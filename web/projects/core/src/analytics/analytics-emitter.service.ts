/**
 * Analytics batching emitter (Step 16) — the **single emission path** for every consumer-originated
 * analytics event. It collects the {@link RawEventBody} payloads the event builders (Step 8) produce
 * and flushes them as one batch to `POST /analytics/events` via the {@link INGEST_ANALYTICS} ingest
 * operation (Step 5), closing §5.9 → §7.5.
 *
 * **Best-effort / non-blocking (the load-bearing rule).** Analytics must never break the user flow:
 * every flush swallows its failure (the error never surfaces into the UI), the buffer is cleared on
 * flush, and a failed batch is **dropped** rather than retried-forever (a stuck queue would grow
 * unbounded). Emitting is fire-and-forget — callers never await it.
 *
 * **Batching.** Events accumulate in an in-memory buffer; a flush is triggered when either
 *   - the buffer reaches {@link FLUSH_BATCH_SIZE} (size-based), or
 *   - {@link FLUSH_DEBOUNCE_MS} elapses after the most recent emit (time-based), or
 *   - the page is hidden / unloaded (`visibilitychange` → hidden, `pagehide`) so in-flight events are
 *     not lost when the user navigates away.
 *
 * **Kind safety (invariant #11 / contract).** The buffer only ever holds events the builders produce,
 * whose `kind` is one of the backend's eight {@link EVENT_KINDS} — there is **no `geo` kind** (the
 * geolocation opt-in rides as `latitude`/`longitude` fields on `search`/`session`). The emitter
 * additionally **drops** any event whose kind is not in that set as a defensive guard, so a bad caller
 * can never make the ingest endpoint 400 the whole batch on an unknown kind. The snake_case→wire-name
 * conversion stays the data layer's job (Step 5), not the emitter's.
 *
 * **Impression dedupe.** {@link emitImpressionsOnce} guards "one impression per shown card per result
 * set": it keys seen impressions by `resultSetToken` + restaurant id, so a results container that
 * re-renders the same ranked list (e.g. on change detection) emits each card's impression exactly
 * once; a new result set (new token) starts a fresh dedupe scope.
 *
 * Framework-light and RN-portable: an injectable service with no view coupling; the page-lifecycle
 * hooks live behind a tiny abstraction the RN rewrite swaps for `AppState`.
 */
import { DestroyRef, Injectable, inject } from '@angular/core';
import { EMPTY } from 'rxjs';
import { catchError } from 'rxjs/operators';

import { EVENT_KINDS, type RawEventBody } from '../domain';
import { INGEST_ANALYTICS } from '../data-access/tokens';

/** Flush once the buffer reaches this many events (size-based trigger). */
export const FLUSH_BATCH_SIZE = 20;

/** Flush this many ms after the most recent emit if the size threshold was not reached (time-based). */
export const FLUSH_DEBOUNCE_MS = 5_000;

/** The eight backend kinds as a lookup, for the defensive unknown-kind guard. */
const KNOWN_KINDS = new Set<string>(EVENT_KINDS);

@Injectable({ providedIn: 'root' })
export class AnalyticsEmitterService {
  private readonly ingest = inject(INGEST_ANALYTICS);
  private readonly destroyRef = inject(DestroyRef);

  /** Pending events awaiting the next flush. */
  private buffer: RawEventBody[] = [];

  /** The active time-based flush timer handle, or `null` when no flush is scheduled. */
  private flushTimer: ReturnType<typeof setTimeout> | null = null;

  /** Restaurant ids whose impression has already been emitted, scoped to {@link impressionToken}. */
  private readonly seenImpressions = new Set<string>();

  /** The result-set token the current {@link seenImpressions} dedupe scope belongs to. */
  private impressionToken: string | null = null;

  /** Bound page-lifecycle listener, kept so it can be removed on destroy. */
  private readonly onPageHide = (): void => this.flush();

  constructor() {
    this.registerPageLifecycleFlush();
    // Flush whatever is buffered when the providing injector is torn down (e.g. in tests / app close).
    this.destroyRef.onDestroy(() => {
      this.unregisterPageLifecycleFlush();
      this.flush();
    });
  }

  /**
   * Buffer one or more built events for the next batch flush. Events whose `kind` is not one of the
   * backend's eight (a defensive guard — the builders never produce such) are dropped so the ingest
   * batch can never 400 on an unknown kind. A size-threshold breach flushes immediately; otherwise a
   * debounced time-based flush is (re)scheduled. Fire-and-forget — never throws into the caller.
   */
  emit(...events: readonly RawEventBody[]): void {
    const accepted = events.filter((e) => KNOWN_KINDS.has(e.kind));
    if (accepted.length === 0) {
      return;
    }
    this.buffer.push(...accepted);

    if (this.buffer.length >= FLUSH_BATCH_SIZE) {
      this.flush();
      return;
    }
    this.scheduleFlush();
  }

  /**
   * Emit one `impression` event per card in `impressions`, **deduped per result set**: within the
   * same `resultSetToken` each restaurant's impression is emitted at most once, so a re-render of the
   * same ranked list does not double-count. A new token resets the dedupe scope (a fresh result set).
   *
   * The caller passes already-built impression events (from the Step 8 builder) so the emitter stays
   * builder-agnostic; it reads each event's `restaurantId` as the dedupe key.
   */
  emitImpressionsOnce(resultSetToken: string, impressions: readonly RawEventBody[]): void {
    if (resultSetToken !== this.impressionToken) {
      this.impressionToken = resultSetToken;
      this.seenImpressions.clear();
    }
    const fresh = impressions.filter((e) => {
      const key = e.restaurantId ?? undefined;
      if (key === undefined || this.seenImpressions.has(key)) {
        return false;
      }
      this.seenImpressions.add(key);
      return true;
    });
    if (fresh.length > 0) {
      this.emit(...fresh);
    }
  }

  /**
   * Flush the buffered events as one batch to the ingest endpoint, best-effort. Clears the buffer and
   * any pending timer up-front so a slow/failed flush cannot block or re-fire; a failure is swallowed
   * (the batch is dropped, never retried into an unbounded queue) so the user flow is never affected.
   */
  flush(): void {
    this.clearTimer();
    if (this.buffer.length === 0) {
      return;
    }
    const batch = this.buffer;
    this.buffer = [];

    this.ingest
      .execute(batch)
      .pipe(catchError(() => EMPTY))
      .subscribe();
  }

  // ── Internals ───────────────────────────────────────────────────────────────

  /** (Re)schedule the debounced time-based flush, resetting the window on each new emit. */
  private scheduleFlush(): void {
    this.clearTimer();
    this.flushTimer = setTimeout(() => {
      this.flushTimer = null;
      this.flush();
    }, FLUSH_DEBOUNCE_MS);
  }

  private clearTimer(): void {
    if (this.flushTimer !== null) {
      clearTimeout(this.flushTimer);
      this.flushTimer = null;
    }
  }

  /**
   * Flush on page hide/unload so events buffered when the user navigates away are not lost.
   * `pagehide` covers tab close / bfcache; `visibilitychange` → hidden covers tab switch / mobile
   * background. Both are best-effort; `document`/`addEventListener` are guarded for non-DOM contexts.
   */
  private registerPageLifecycleFlush(): void {
    const doc = globalThis.document as Document | undefined;
    if (doc === undefined || typeof doc.addEventListener !== 'function') {
      return;
    }
    doc.addEventListener('visibilitychange', this.onVisibilityChange);
    globalThis.addEventListener?.('pagehide', this.onPageHide);
  }

  private unregisterPageLifecycleFlush(): void {
    const doc = globalThis.document as Document | undefined;
    doc?.removeEventListener?.('visibilitychange', this.onVisibilityChange);
    globalThis.removeEventListener?.('pagehide', this.onPageHide);
  }

  /** Flush only when the page is actually being hidden (not on becoming visible again). */
  private readonly onVisibilityChange = (): void => {
    if ((globalThis.document as Document | undefined)?.visibilityState === 'hidden') {
      this.flush();
    }
  };
}
