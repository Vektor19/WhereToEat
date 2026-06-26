import { TestBed, type ComponentFixture } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { Subject, of } from 'rxjs';
import {
  AnalyticsEmitterService,
  AnalyticsEventBuilders,
  GET_MAP,
  INGEST_ANALYTICS,
  MAP_PROVIDER,
  type MapPayloadDto,
  type MapProvider,
  type MapView,
  type RawEventBody,
} from 'core';

import { MapModalComponent } from './map-modal.component';

function payload(over: Partial<MapPayloadDto> = {}): MapPayloadDto {
  return {
    restaurantId: 'r-1',
    hasMapData: true,
    latitude: 50.45,
    longitude: 30.52,
    placeId: 'PID',
    mapsDeepLink: null,
    ...over,
  };
}

/** A stub provider that returns whatever view the test sets — isolates the modal from real providers. */
class StubMapProvider implements MapProvider {
  view: MapView = { kind: 'none' };
  resolve(): MapView {
    return this.view;
  }
}

describe('MapModalComponent', () => {
  let map$: Subject<MapPayloadDto>;
  let ingestBatches: RawEventBody[][];
  let provider: StubMapProvider;

  beforeEach(async () => {
    map$ = new Subject<MapPayloadDto>();
    ingestBatches = [];
    provider = new StubMapProvider();

    await TestBed.configureTestingModule({
      imports: [MapModalComponent],
      providers: [
        provideNoopAnimations(),
        AnalyticsEventBuilders,
        { provide: GET_MAP, useValue: { execute: () => map$.asObservable() } },
        { provide: MAP_PROVIDER, useValue: provider },
        {
          provide: INGEST_ANALYTICS,
          useValue: {
            execute: (events: readonly RawEventBody[]) => {
              ingestBatches.push([...events]);
              return of(undefined);
            },
          },
        },
      ],
    }).compileComponents();
  });

  function render(
    over: { restaurantId?: string; name?: string } = {},
  ): ComponentFixture<MapModalComponent> {
    const fixture = TestBed.createComponent(MapModalComponent);
    fixture.componentRef.setInput('restaurantId', over.restaurantId ?? 'r-1');
    fixture.componentRef.setInput('name', over.name ?? 'Smачно');
    fixture.detectChanges();
    return fixture;
  }

  function el(fixture: ComponentFixture<MapModalComponent>, testid: string): Element | null {
    return fixture.nativeElement.querySelector(`[data-testid="${testid}"]`);
  }

  it('fetches the live payload and renders the active provider as a live embed', () => {
    provider.view = { kind: 'embed', url: 'https://www.google.com/maps/embed/v1/place?key=K' };
    const fixture = render();
    // Loading shows until the payload lands.
    expect(el(fixture, 'loading')).not.toBeNull();

    map$.next(payload());
    map$.complete();
    fixture.detectChanges();

    const iframe = el(fixture, 'map-embed') as HTMLIFrameElement | null;
    expect(iframe).not.toBeNull();
    // iframe is sandboxed and uses a no-referrer policy (live, safe embed).
    expect(iframe?.getAttribute('sandbox')).toContain('allow-scripts');
    expect(iframe?.getAttribute('referrerpolicy')).toBe('no-referrer');
  });

  it('renders the deep-link affordance when the provider degrades (no credential path)', () => {
    provider.view = { kind: 'deep-link', url: 'https://maps.app.goo.gl/x' };
    const fixture = render();
    map$.next(payload());
    map$.complete();
    fixture.detectChanges();

    const link = el(fixture, 'map-deeplink-link') as HTMLAnchorElement | null;
    expect(link).not.toBeNull();
    expect(link?.getAttribute('href')).toBe('https://maps.app.goo.gl/x');
    expect(el(fixture, 'map-embed')).toBeNull();
  });

  it('renders the no-map state for a hasMapData=false payload', () => {
    provider.view = { kind: 'none' };
    const fixture = render();
    map$.next(payload({ hasMapData: false }));
    map$.complete();
    fixture.detectChanges();

    // The shared error/empty primitive renders the well-defined "no map data" state.
    expect(el(fixture, 'error-state')).not.toBeNull();
    expect(el(fixture, 'map-embed')).toBeNull();
    expect(el(fixture, 'map-deeplink')).toBeNull();
  });

  it('renders the error state when the map fetch fails', () => {
    const fixture = render();
    map$.error({
      kind: 'notFound',
      status: 404,
      code: 'NotFound',
      message: 'missing',
      messageKey: 'errors.notFound',
    });
    fixture.detectChanges();
    expect(el(fixture, 'error-state')).not.toBeNull();
  });

  it('is an ARIA dialog with a labelled title (a11y)', () => {
    const fixture = render({ name: 'Borscht House' });
    const dialog = el(fixture, 'map-dialog');
    expect(dialog?.getAttribute('role')).toBe('dialog');
    expect(dialog?.getAttribute('aria-modal')).toBe('true');
    expect(dialog?.getAttribute('aria-label')).toContain('Borscht House');
  });

  it('emits an action analytics event once on open', () => {
    render();
    TestBed.inject(AnalyticsEmitterService).flush();
    const actions = ingestBatches.flat().filter((e) => e.kind === 'action');
    expect(actions.length).toBe(1);
    expect(actions[0].restaurantId).toBe('r-1');
  });

  it('does not re-emit the action event when reload re-fetches the map', () => {
    provider.view = { kind: 'embed', url: 'https://www.google.com/maps/embed/v1/place?key=K' };
    const fixture = render();
    const emitter = TestBed.inject(AnalyticsEmitterService);

    // First payload lands — exactly one `action` event for this open.
    map$.next(payload());
    fixture.detectChanges();
    emitter.flush();
    expect(ingestBatches.flat().filter((e) => e.kind === 'action').length).toBe(1);

    // A retry re-runs the fetch; delivering a second payload must keep the count at one
    // (the actionEmitted guard fires the `action` event once per open, not per fetch).
    fixture.componentInstance.reload();
    map$.next(payload());
    fixture.detectChanges();
    emitter.flush();

    expect(ingestBatches.flat().filter((e) => e.kind === 'action').length).toBe(1);
  });

  it('emits closed on the close button, ESC, and a backdrop click', () => {
    const fixture = render();
    let closes = 0;
    fixture.componentInstance.closed.subscribe(() => (closes += 1));

    (el(fixture, 'map-close') as HTMLButtonElement).click();
    expect(closes).toBe(1);

    // ESC anywhere in the dialog (host listener).
    (el(fixture, 'map-dialog') as HTMLElement).dispatchEvent(
      new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }),
    );
    expect(closes).toBe(2);

    // A click on the backdrop itself (not the dialog) closes too.
    const backdrop = el(fixture, 'map-backdrop') as HTMLElement;
    backdrop.dispatchEvent(new MouseEvent('click', { bubbles: true }));
    expect(closes).toBe(3);
  });

  it('does not close on a click inside the dialog (only the backdrop itself)', () => {
    const fixture = render();
    let closes = 0;
    fixture.componentInstance.closed.subscribe(() => (closes += 1));

    (el(fixture, 'map-dialog') as HTMLElement).dispatchEvent(
      new MouseEvent('click', { bubbles: true }),
    );
    expect(closes).toBe(0);
  });
});
