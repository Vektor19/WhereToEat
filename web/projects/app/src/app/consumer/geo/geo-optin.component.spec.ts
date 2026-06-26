import { TestBed, type ComponentFixture } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import {
  GEO_MESSAGE_KEYS,
  GeoOptInStore,
  GeolocationService,
  SelectionStore,
  type GeolocationResult,
} from 'core';

import { GeoOptInComponent } from './geo-optin.component';

/** A mock geolocation port whose next result the test controls (no real `navigator`). */
class MockGeolocationService {
  next: GeolocationResult = { ok: true, coordinate: { latitude: 50.45, longitude: 30.52 } };
  calls = 0;

  acquire(): Promise<GeolocationResult> {
    this.calls += 1;
    return Promise.resolve(this.next);
  }
}

describe('GeoOptInComponent', () => {
  let geo: MockGeolocationService;
  let selection: SelectionStore;
  let optInStore: GeoOptInStore;

  function setup(): ComponentFixture<GeoOptInComponent> {
    geo = new MockGeolocationService();
    TestBed.configureTestingModule({
      imports: [GeoOptInComponent],
      providers: [provideNoopAnimations(), { provide: GeolocationService, useValue: geo }],
    });
    selection = TestBed.inject(SelectionStore);
    optInStore = TestBed.inject(GeoOptInStore);
    const fixture = TestBed.createComponent(GeoOptInComponent);
    fixture.detectChanges();
    return fixture;
  }

  beforeEach(() => {
    localStorage.clear();
    TestBed.resetTestingModule();
  });

  afterEach(() => localStorage.clear());

  function toggle(fixture: ComponentFixture<GeoOptInComponent>, checked: boolean): void {
    // Drive the component handler directly — the slide-toggle's change carries `{ checked }`.
    fixture.componentInstance.onToggle(checked);
  }

  it('is off by default and does NOT call geolocation until opt-in (invariant #11)', () => {
    setup();
    expect(geo.calls).toBe(0);
    expect(selection.userGeo()).toBeUndefined();
    expect(optInStore.optedIn()).toBe(false);
  });

  it('opting in acquires approximate coords, sets userGeo, and the built request includes a valid userGeo', async () => {
    const fixture = setup();
    selection.addItem({ dishId: 'd-1' });

    toggle(fixture, true);
    await fixture.whenStable();

    expect(geo.calls).toBe(1);
    expect(selection.userGeo()).toEqual({ latitude: 50.45, longitude: 30.52 });
    expect(optInStore.optedIn()).toBe(true);

    const build = selection.buildRequest();
    expect(build.valid).toBe(true);
    if (build.valid) {
      expect(build.request.userGeo).toEqual({ latitude: 50.45, longitude: 30.52 });
    }
  });

  it('toggling off clears userGeo so the built request OMITS it (invariant #11)', async () => {
    const fixture = setup();
    selection.addItem({ dishId: 'd-1' });

    toggle(fixture, true);
    await fixture.whenStable();
    expect(selection.userGeo()).toBeDefined();

    toggle(fixture, false);

    expect(selection.userGeo()).toBeUndefined();
    expect(optInStore.optedIn()).toBe(false);
    const build = selection.buildRequest();
    expect(build.valid).toBe(true);
    if (build.valid) {
      expect('userGeo' in build.request).toBe(false);
    }
  });

  it('a denied/unavailable acquisition surfaces a handled message key and does NOT opt in (no crash)', async () => {
    const fixture = setup();
    geo.next = {
      ok: false,
      reason: 'permission-denied',
      messageKey: GEO_MESSAGE_KEYS['permission-denied'],
    };

    toggle(fixture, true);
    await fixture.whenStable();
    fixture.detectChanges();

    expect(selection.userGeo()).toBeUndefined();
    expect(optInStore.optedIn()).toBe(false);
    const error = (fixture.nativeElement as HTMLElement).querySelector(
      '[data-testid="geo-error"]',
    ) as HTMLElement;
    expect(error).not.toBeNull();
    expect(error.getAttribute('data-message-key')).toBe(GEO_MESSAGE_KEYS['permission-denied']);
  });

  it('renders an accessible labelled toggle and a discreet consent message', () => {
    const fixture = setup();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('[data-testid="geo-toggle"]')).not.toBeNull();
    const consent = el.querySelector('[data-testid="geo-consent"]') as HTMLElement;
    expect(consent).not.toBeNull();
    // Honest, discreet copy: approximate, optional, not sold (invariant #11 / §10).
    expect(consent.textContent).toContain('приблизне');
    expect(consent.textContent).toContain('не продаємо');
  });

  it('re-honours a persisted opt-in on init by silently re-acquiring (coords not persisted)', async () => {
    // Seed a prior session's opt-in choice.
    new GeoOptInStore().optIn();
    const fixture = setup();
    await fixture.whenStable();

    expect(geo.calls).toBe(1);
    expect(selection.userGeo()).toEqual({ latitude: 50.45, longitude: 30.52 });
    expect(optInStore.optedIn()).toBe(true);
  });

  it('reverts a persisted opt-in to off (no error toast) when permission was revoked between sessions', async () => {
    // Seed a prior opt-in, then have this session's silent re-acquire be denied.
    localStorage.setItem('dp.geo.optIn', 'true');
    geo = new MockGeolocationService();
    geo.next = {
      ok: false,
      reason: 'permission-denied',
      messageKey: GEO_MESSAGE_KEYS['permission-denied'],
    };
    TestBed.configureTestingModule({
      imports: [GeoOptInComponent],
      providers: [provideNoopAnimations(), { provide: GeolocationService, useValue: geo }],
    });
    selection = TestBed.inject(SelectionStore);
    optInStore = TestBed.inject(GeoOptInStore);
    const fixture = TestBed.createComponent(GeoOptInComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(selection.userGeo()).toBeUndefined();
    expect(optInStore.optedIn()).toBe(false);
    // The silent init re-acquire shows no error toast.
    expect(
      (fixture.nativeElement as HTMLElement).querySelector('[data-testid="geo-error"]'),
    ).toBeNull();
  });
});
