import {
  EVENT_KINDS,
  EVENT_KIND_WIRE_NAME,
  type EventKind,
  type RawEventBody,
} from './analytics.models';

/**
 * The backend `Analytics.Domain.EventKind` enum, transcribed by member name in declaration order
 * (`Impression = 0` … `Session = 7`). The in-app union must map 1:1 onto this set — **no `geo`
 * member, no extras** — because the ingest endpoint 400s the whole batch on an unknown kind.
 */
const BACKEND_EVENT_KINDS = [
  'Impression',
  'View',
  'CardOpen',
  'Action',
  'Search',
  'Filter',
  'RatingGiven',
  'Session',
] as const;

describe('EventKind', () => {
  it("has exactly the backend enum's eight members (no geo, no extras)", () => {
    expect(EVENT_KINDS.length).toBe(8);
    expect(EVENT_KINDS.length).toBe(BACKEND_EVENT_KINDS.length);
  });

  it('contains no `geo` member (the backend enum has none; it would 400 the batch)', () => {
    expect(EVENT_KINDS).not.toContain('geo' as EventKind);
    expect(Object.keys(EVENT_KIND_WIRE_NAME)).not.toContain('geo');
  });

  it('includes rating_given (mirroring backend RatingGiven = 6)', () => {
    expect(EVENT_KINDS).toContain('rating_given');
    expect(EVENT_KIND_WIRE_NAME.rating_given).toBe('RatingGiven');
  });

  it('maps 1:1 onto the backend enum via the wire-name table', () => {
    // Every in-app kind has a wire name, and the set of wire names equals the backend member set.
    const wireNames = EVENT_KINDS.map((kind) => EVENT_KIND_WIRE_NAME[kind]);

    expect([...wireNames].sort()).toEqual([...BACKEND_EVENT_KINDS].sort());
    // Bijection: no duplicate wire names, exactly eight distinct.
    expect(new Set(wireNames).size).toBe(8);
  });

  it('wire names use the C# enum-member spelling, not the snake_case union value', () => {
    // The parser is case-insensitive but does NOT strip underscores — `card_open` would 400.
    expect(EVENT_KIND_WIRE_NAME.card_open).toBe('CardOpen');
    expect(EVENT_KIND_WIRE_NAME.impression).toBe('Impression');
    expect(EVENT_KIND_WIRE_NAME.session).toBe('Session');
  });
});

describe('RawEventBody', () => {
  it('accepts a kind-only event (every other field optional)', () => {
    const event: RawEventBody = { kind: 'session' };

    expect(event.kind).toBe('session');
  });

  it('carries the optional geolocation as latitude/longitude fields, not a geo kind', () => {
    const event: RawEventBody = { kind: 'search', latitude: 50.45, longitude: 30.52 };

    expect(event.kind).toBe('search');
    expect(event.latitude).toBe(50.45);
    expect(event.longitude).toBe(30.52);
  });
});
