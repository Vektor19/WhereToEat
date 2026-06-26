import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';

import { EVENT_KINDS } from '../domain';
import type { EventKind, RawEventBody } from '../domain';
import { INGEST_ANALYTICS } from '../data-access/tokens';
import {
  AnalyticsEmitterService,
  FLUSH_BATCH_SIZE,
  FLUSH_DEBOUNCE_MS,
} from './analytics-emitter.service';

/** A recording ingest stub: captures every flushed batch and lets a test force a failure. */
class StubIngest {
  readonly batches: RawEventBody[][] = [];
  fail = false;
  execute(events: readonly RawEventBody[]) {
    this.batches.push([...events]);
    return this.fail ? throwError(() => new Error('ingest down')) : of(undefined);
  }
  /** Total events flushed across all batches. */
  total(): number {
    return this.batches.reduce((n, b) => n + b.length, 0);
  }
}

function event(kind: EventKind, restaurantId?: string): RawEventBody {
  return {
    kind,
    occurredAtUtc: '2026-06-21T00:00:00.000Z',
    sessionId: 'session-fixed-id',
    ...(restaurantId === undefined ? {} : { restaurantId }),
  };
}

function createEmitter(ingest: StubIngest): AnalyticsEmitterService {
  TestBed.configureTestingModule({
    providers: [AnalyticsEmitterService, { provide: INGEST_ANALYTICS, useValue: ingest }],
  });
  return TestBed.inject(AnalyticsEmitterService);
}

describe('AnalyticsEmitterService', () => {
  let ingest: StubIngest;
  let emitter: AnalyticsEmitterService;

  beforeEach(() => {
    ingest = new StubIngest();
    emitter = createEmitter(ingest);
  });

  it('batches multiple events and flushes them as one batch to the ingest port', () => {
    emitter.emit(event('view', 'r-1'));
    emitter.emit(event('card_open', 'r-1'));
    expect(ingest.batches.length).toBe(0); // buffered, not yet flushed

    emitter.flush();

    expect(ingest.batches.length).toBe(1);
    expect(ingest.batches[0].length).toBe(2);
    expect(ingest.batches[0].map((e) => e.kind)).toEqual(['view', 'card_open']);
  });

  it('flushes immediately when the buffer reaches the size threshold', () => {
    for (let i = 0; i < FLUSH_BATCH_SIZE; i += 1) {
      emitter.emit(event('impression', `r-${i}`));
    }
    expect(ingest.batches.length).toBe(1);
    expect(ingest.batches[0].length).toBe(FLUSH_BATCH_SIZE);
  });

  it('flushes on the debounce timer when the size threshold is not reached', () => {
    vi.useFakeTimers();
    try {
      emitter.emit(event('search'));
      emitter.emit(event('filter'));
      expect(ingest.batches.length).toBe(0);

      vi.advanceTimersByTime(FLUSH_DEBOUNCE_MS);
      expect(ingest.batches.length).toBe(1);
      expect(ingest.batches[0].length).toBe(2);
    } finally {
      vi.useRealTimers();
    }
  });

  it('flushes on page hide (visibilitychange → hidden)', () => {
    emitter.emit(event('action', 'r-1'));
    Object.defineProperty(document, 'visibilityState', {
      configurable: true,
      get: () => 'hidden',
    });
    document.dispatchEvent(new Event('visibilitychange'));
    expect(ingest.batches.length).toBe(1);
    expect(ingest.batches[0].length).toBe(1);
  });

  it('flushes on page unload (pagehide — tab close / bfcache)', () => {
    emitter.emit(event('action', 'r-1'));
    expect(ingest.batches.length).toBe(0); // buffered, not yet flushed

    window.dispatchEvent(new Event('pagehide'));

    expect(ingest.batches.length).toBe(1);
    expect(ingest.batches[0].length).toBe(1);
    expect(ingest.batches[0][0].restaurantId).toBe('r-1');
  });

  it('swallows an ingest failure so the user flow is unaffected (no throw, buffer cleared)', () => {
    ingest.fail = true;
    emitter.emit(event('view', 'r-1'));
    expect(() => emitter.flush()).not.toThrow();
    expect(ingest.batches.length).toBe(1); // attempted once

    // A failed batch is dropped (not retried): a fresh emit + flush does not resend the old event.
    ingest.fail = false;
    emitter.emit(event('card_open', 'r-2'));
    emitter.flush();
    expect(ingest.batches.length).toBe(2);
    expect(ingest.batches[1].map((e) => e.restaurantId)).toEqual(['r-2']);
  });

  it('drops an unknown kind so the ingest batch can never 400 on it', () => {
    emitter.emit({ kind: 'geo' as unknown as EventKind, restaurantId: 'r-1' });
    emitter.emit(event('view', 'r-2'));
    emitter.flush();

    expect(ingest.total()).toBe(1);
    expect(ingest.batches[0][0].kind).toBe('view');
  });

  it('only ever flushes events whose kind is one of the eight backend kinds (never geo)', () => {
    for (const kind of EVENT_KINDS) {
      emitter.emit(event(kind, 'r'));
    }
    emitter.flush();
    const flushed = ingest.batches.flat();
    expect(flushed.length).toBe(EVENT_KINDS.length);
    for (const e of flushed) {
      expect((EVENT_KINDS as readonly string[]).includes(e.kind)).toBe(true);
      expect(e.kind).not.toBe('geo');
    }
  });

  it('dedupes impressions to one per card per result set', () => {
    const set1 = [event('impression', 'r-1'), event('impression', 'r-2')];
    emitter.emitImpressionsOnce('set-1', set1);
    emitter.emitImpressionsOnce('set-1', set1); // a re-render of the same set
    emitter.flush();
    expect(ingest.total()).toBe(2); // r-1, r-2 once each

    // A new result set resets the dedupe scope, so r-1 is impressed again.
    emitter.emitImpressionsOnce('set-2', [event('impression', 'r-1')]);
    emitter.flush();
    expect(ingest.total()).toBe(3);
  });
});
