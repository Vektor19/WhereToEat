import { TestBed } from '@angular/core/testing';

import { EVENT_KINDS } from '../domain';
import type { EventKind, RawEventBody } from '../domain';
import { AnalyticsEventBuilders } from './event-builders';
import { SessionIdProvider } from './session-id.provider';

/** A stub session-id provider returning a fixed opaque id so payloads are deterministic. */
class StubSessionIdProvider {
  static readonly ID = 'session-fixed-id';
  getSessionId(): string {
    return StubSessionIdProvider.ID;
  }
  reset(): void {
    /* no-op */
  }
}

function createBuilders(): AnalyticsEventBuilders {
  TestBed.configureTestingModule({
    providers: [
      AnalyticsEventBuilders,
      { provide: SessionIdProvider, useClass: StubSessionIdProvider },
    ],
  });
  return TestBed.inject(AnalyticsEventBuilders);
}

describe('AnalyticsEventBuilders', () => {
  let builders: AnalyticsEventBuilders;

  beforeEach(() => {
    builders = createBuilders();
  });

  /** Common assertions every built event must satisfy: known kind, session id, no PII. */
  function expectCommon(event: RawEventBody, kind: EventKind): void {
    expect(event.kind).toBe(kind);
    expect((EVENT_KINDS as readonly string[]).includes(event.kind)).toBe(true);
    expect(event.sessionId).toBe(StubSessionIdProvider.ID);
    expect(typeof event.occurredAtUtc).toBe('string');
    // No PII: no user identity is stamped client-side.
    expect(event.userId ?? null).toBeNull();
  }

  it('builds an impression event with restaurant + position', () => {
    const event = builders.impression({ restaurantId: 'r-1', position: 3 });
    expectCommon(event, 'impression');
    expect(event.restaurantId).toBe('r-1');
    expect(event.position).toBe(3);
  });

  it('builds a view event', () => {
    const event = builders.view({ restaurantId: 'r-1', position: 0 });
    expectCommon(event, 'view');
    expect(event.restaurantId).toBe('r-1');
  });

  it('builds a card_open event (snake_case in-app kind)', () => {
    const event = builders.cardOpen({ restaurantId: 'r-1' });
    expectCommon(event, 'card_open');
    expect(event.restaurantId).toBe('r-1');
  });

  it('builds an action event', () => {
    const event = builders.action({ restaurantId: 'r-1' });
    expectCommon(event, 'action');
    expect(event.restaurantId).toBe('r-1');
  });

  it('builds a search event with category/dish ids and sort mode', () => {
    const event = builders.search({
      categoryIds: ['c-1'],
      dishIds: ['d-1', 'd-2'],
      sortMode: 'price',
    });
    expectCommon(event, 'search');
    expect(event.categoryIds).toEqual(['c-1']);
    expect(event.dishIds).toEqual(['d-1', 'd-2']);
    expect(event.sortMode).toBe('price');
    // No geo fields when geo was not opted in.
    expect(event.latitude ?? null).toBeNull();
    expect(event.longitude ?? null).toBeNull();
  });

  it('attaches geo as latitude/longitude FIELDS on a search event (never a geo kind)', () => {
    const event = builders.search({
      categoryIds: ['c-1'],
      geo: { latitude: 50.45, longitude: 30.52 },
    });
    expect(event.kind).toBe('search');
    expect(event.latitude).toBe(50.45);
    expect(event.longitude).toBe(30.52);
  });

  it('builds a filter event with the applied filter keys', () => {
    const event = builders.filter({ filters: ['price', 'rating'], sortMode: 'best' });
    expectCommon(event, 'filter');
    expect(event.filters).toEqual(['price', 'rating']);
    expect(event.sortMode).toBe('best');
  });

  it('builds a rating_given event', () => {
    const event = builders.ratingGiven({ restaurantId: 'r-1' });
    expectCommon(event, 'rating_given');
    expect(event.restaurantId).toBe('r-1');
  });

  it('builds a session event, optionally carrying geo as fields', () => {
    const plain = builders.session();
    expectCommon(plain, 'session');
    expect(plain.latitude ?? null).toBeNull();

    const withGeo = builders.session({ geo: { latitude: 49.84, longitude: 24.03 } });
    expect(withGeo.kind).toBe('session');
    expect(withGeo.latitude).toBe(49.84);
    expect(withGeo.longitude).toBe(24.03);
  });

  it('never produces a "geo" kind (the backend enum has none — would 400 the batch)', () => {
    const all: RawEventBody[] = [
      builders.impression({ restaurantId: 'r', position: 1 }),
      builders.view({ restaurantId: 'r' }),
      builders.cardOpen({ restaurantId: 'r' }),
      builders.action({ restaurantId: 'r' }),
      builders.search({ geo: { latitude: 1, longitude: 2 } }),
      builders.filter({ filters: ['price'] }),
      builders.ratingGiven({ restaurantId: 'r' }),
      builders.session({ geo: { latitude: 1, longitude: 2 } }),
    ];
    for (const event of all) {
      expect(event.kind).not.toBe('geo' as unknown as EventKind);
      expect((EVENT_KINDS as readonly string[]).includes(event.kind)).toBe(true);
    }
  });

  it('omits empty id arrays rather than sending empty lists', () => {
    const event = builders.search({ categoryIds: [], dishIds: [] });
    expect(event.categoryIds ?? null).toBeNull();
    expect(event.dishIds ?? null).toBeNull();
  });

  it('does not alias the caller arrays (defensive copy)', () => {
    const categoryIds = ['c-1'];
    const event = builders.search({ categoryIds });
    expect(event.categoryIds).not.toBe(categoryIds);
    expect(event.categoryIds).toEqual(categoryIds);
  });
});
