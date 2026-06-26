import { Injector, runInInjectionContext } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import {
  AnalyticsEmitterService,
  AnalyticsEventBuilders,
  GeoOptInStore,
  SelectionStore,
  type RawEventBody,
  type UserGeo,
} from 'core';

import { emitSessionStart } from './app.config';

/** A recording emitter stub: captures every emitted event so the session emission can be asserted. */
class EmitterStub {
  readonly emitted: RawEventBody[] = [];
  emit(...events: readonly RawEventBody[]): void {
    this.emitted.push(...events);
  }
}

/** A geo opt-in stub whose `optedIn` is fixed per test. */
class GeoOptInStub {
  constructor(private readonly value: boolean) {}
  optedIn(): boolean {
    return this.value;
  }
}

/** A selection-store stub exposing only the `userGeo` signal the session emission reads. */
class SelectionStub {
  constructor(private readonly geo: UserGeo | undefined) {}
  userGeo(): UserGeo | undefined {
    return this.geo;
  }
}

function setup(optedIn: boolean, geo: UserGeo | undefined): EmitterStub {
  const emitter = new EmitterStub();
  TestBed.configureTestingModule({
    providers: [
      AnalyticsEventBuilders,
      { provide: AnalyticsEmitterService, useValue: emitter },
      { provide: GeoOptInStore, useValue: new GeoOptInStub(optedIn) },
      { provide: SelectionStore, useValue: new SelectionStub(geo) },
    ],
  });
  const injector = TestBed.inject(Injector);
  runInInjectionContext(injector, () => emitSessionStart());
  return emitter;
}

describe('emitSessionStart (session-boundary analytics)', () => {
  it('emits exactly one session event on init', () => {
    const emitter = setup(false, undefined);
    expect(emitter.emitted.length).toBe(1);
    expect(emitter.emitted[0].kind).toBe('session');
  });

  it('omits geo fields when the user has not opted in (even if a coordinate exists)', () => {
    const emitter = setup(false, { latitude: 50.45, longitude: 30.52 });
    const session = emitter.emitted[0];
    expect(session.kind).toBe('session');
    expect(session.latitude).toBeUndefined();
    expect(session.longitude).toBeUndefined();
  });

  it('carries geo fields (never a geo kind) when opted in with a coordinate', () => {
    const emitter = setup(true, { latitude: 50.45, longitude: 30.52 });
    const session = emitter.emitted[0];
    expect(session.kind).toBe('session');
    expect(session.kind).not.toBe('geo');
    expect(session.latitude).toBe(50.45);
    expect(session.longitude).toBe(30.52);
  });

  it('omits geo fields when opted in but no coordinate has been acquired yet', () => {
    const emitter = setup(true, undefined);
    const session = emitter.emitted[0];
    expect(session.kind).toBe('session');
    expect(session.latitude).toBeUndefined();
    expect(session.longitude).toBeUndefined();
  });
});
