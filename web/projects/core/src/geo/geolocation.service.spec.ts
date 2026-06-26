import { TestBed } from '@angular/core/testing';

import { GEO_MESSAGE_KEYS, GeolocationService } from './geolocation.service';

/** Minimal `GeolocationPositionError` stand-in carrying the W3C numeric codes. */
function positionError(code: number): GeolocationPositionError {
  return {
    code,
    message: '',
    PERMISSION_DENIED: 1,
    POSITION_UNAVAILABLE: 2,
    TIMEOUT: 3,
  } as GeolocationPositionError;
}

describe('GeolocationService', () => {
  let service: GeolocationService;
  let getCurrentPosition: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    getCurrentPosition = vi.fn();
    vi.stubGlobal('navigator', { geolocation: { getCurrentPosition } });
    TestBed.configureTestingModule({});
    service = TestBed.inject(GeolocationService);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('returns approximate coordinates on success and requests low accuracy (no precise tracking)', async () => {
    getCurrentPosition.mockImplementation((success: PositionCallback) =>
      success({ coords: { latitude: 50.45, longitude: 30.52 } } as GeolocationPosition),
    );

    const result = await service.acquire();

    expect(result.ok).toBe(true);
    if (result.ok) {
      expect(result.coordinate).toEqual({ latitude: 50.45, longitude: 30.52 });
    }
    // Invariant #11: approximate only — high accuracy must be off.
    const options = getCurrentPosition.mock.calls[0][2] as PositionOptions;
    expect(options.enableHighAccuracy).toBe(false);
  });

  it('maps permission-denied to a handled failure with its i18n key (no throw)', async () => {
    getCurrentPosition.mockImplementation((_: PositionCallback, error: PositionErrorCallback) =>
      error(positionError(1)),
    );

    const result = await service.acquire();

    expect(result.ok).toBe(false);
    if (!result.ok) {
      expect(result.reason).toBe('permission-denied');
      expect(result.messageKey).toBe(GEO_MESSAGE_KEYS['permission-denied']);
    }
  });

  it('maps position-unavailable to a handled failure', async () => {
    getCurrentPosition.mockImplementation((_: PositionCallback, error: PositionErrorCallback) =>
      error(positionError(2)),
    );

    const result = await service.acquire();

    expect(result.ok).toBe(false);
    if (!result.ok) {
      expect(result.reason).toBe('position-unavailable');
    }
  });

  it('maps timeout to a handled failure', async () => {
    getCurrentPosition.mockImplementation((_: PositionCallback, error: PositionErrorCallback) =>
      error(positionError(3)),
    );

    const result = await service.acquire();

    expect(result.ok).toBe(false);
    if (!result.ok) {
      expect(result.reason).toBe('timeout');
    }
  });

  it('returns an unsupported failure when the platform has no geolocation API', async () => {
    vi.stubGlobal('navigator', {});
    const result = await service.acquire();

    expect(result.ok).toBe(false);
    if (!result.ok) {
      expect(result.reason).toBe('unsupported');
      expect(result.messageKey).toBe(GEO_MESSAGE_KEYS.unsupported);
    }
    expect(getCurrentPosition).not.toHaveBeenCalled();
  });
});
