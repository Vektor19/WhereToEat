import { EARTH_RADIUS_KM, haversineDistanceKm } from './haversine';

describe('haversineDistanceKm', () => {
  it('returns exactly 0 for a point to itself', () => {
    const point = { latitude: 50.45, longitude: 30.52 };
    expect(haversineDistanceKm(point, point)).toBe(0);
  });

  it('returns 0 for distinct objects with identical coordinates', () => {
    expect(
      haversineDistanceKm({ latitude: 10, longitude: 20 }, { latitude: 10, longitude: 20 }),
    ).toBe(0);
  });

  it('computes Kyiv → Lviv (~468 km) within tolerance', () => {
    const kyiv = { latitude: 50.4501, longitude: 30.5234 };
    const lviv = { latitude: 49.8397, longitude: 24.0297 };
    const km = haversineDistanceKm(kyiv, lviv);
    expect(km).toBeGreaterThan(460);
    expect(km).toBeLessThan(475);
  });

  it('computes one degree of latitude (~111.2 km along a meridian)', () => {
    const km = haversineDistanceKm({ latitude: 0, longitude: 0 }, { latitude: 1, longitude: 0 });
    // One degree of arc = EarthRadius * (pi/180) ≈ 111.19 km.
    expect(km).toBeCloseTo((EARTH_RADIUS_KM * Math.PI) / 180, 2);
  });

  it('is symmetric (from→to equals to→from)', () => {
    const a = { latitude: 48.8566, longitude: 2.3522 }; // Paris
    const b = { latitude: 51.5074, longitude: -0.1278 }; // London
    expect(haversineDistanceKm(a, b)).toBeCloseTo(haversineDistanceKm(b, a), 9);
  });

  it('computes a known antipodal-ish long distance (London → New York ~5570 km)', () => {
    const london = { latitude: 51.5074, longitude: -0.1278 };
    const newYork = { latitude: 40.7128, longitude: -74.006 };
    const km = haversineDistanceKm(london, newYork);
    expect(km).toBeGreaterThan(5500);
    expect(km).toBeLessThan(5620);
  });
});
