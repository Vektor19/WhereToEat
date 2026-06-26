import { DistanceFormatter } from './distance.formatter';

describe('DistanceFormatter', () => {
  let formatter: DistanceFormatter;

  beforeEach(() => {
    formatter = new DistanceFormatter();
  });

  it('formats a whole-km distance with a km unit', () => {
    const formatted = formatter.formatKm(3, 'uk');
    expect(formatted).toContain('3');
    expect(formatted.toLowerCase()).toMatch(/км|km/);
  });

  it('keeps one decimal for a sub-kilometre distance (never rounds to 0)', () => {
    const formatted = formatter.formatKm(0.3, 'uk');
    expect(formatted).toMatch(/0[.,]3/);
  });

  it('rounds to at most one fractional digit', () => {
    const formatted = formatter.formatKm(1.27, 'uk');
    expect(formatted).toMatch(/1[.,]3/);
  });

  it('is locale-aware (decimal separator differs by locale)', () => {
    const uk = formatter.formatKm(1.2, 'uk');
    const en = formatter.formatKm(1.2, 'en-US');
    // uk uses a comma decimal, en-US a dot.
    expect(uk).not.toBe(en);
  });

  it('returns an empty string for a negative or non-finite distance', () => {
    expect(formatter.formatKm(-1)).toBe('');
    expect(formatter.formatKm(Number.NaN)).toBe('');
  });

  describe('formatOptionalKm', () => {
    it('formats a present distance', () => {
      expect(formatter.formatOptionalKm(2.5, 'uk')).toMatch(/2[.,]5/);
    });

    it('returns null when distance is absent (geo opt-out — invariant #11)', () => {
      expect(formatter.formatOptionalKm(null)).toBeNull();
      expect(formatter.formatOptionalKm(undefined)).toBeNull();
    });
  });
});
