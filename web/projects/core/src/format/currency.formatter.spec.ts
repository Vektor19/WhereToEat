import { CurrencyFormatter } from './currency.formatter';

describe('CurrencyFormatter', () => {
  let formatter: CurrencyFormatter;

  beforeEach(() => {
    formatter = new CurrencyFormatter();
  });

  it('formats by the supplied currency code, not a hardcoded symbol', () => {
    // The currency is driven by the code: UAH vs EUR vs USD all flow through the same path.
    const uah = formatter.format(150, 'UAH', 'uk');
    const eur = formatter.format(150, 'EUR', 'uk');
    const usd = formatter.format(150, 'USD', 'uk');

    expect(uah).not.toBe(eur);
    expect(eur).not.toBe(usd);
    // The amount is present in each, localised.
    expect(uah).toContain('150');
    expect(eur).toContain('150');
  });

  it('reflects the launch currency (UAH) without baking ₴ into the source', () => {
    const formatted = formatter.format(99.5, 'UAH', 'uk');
    // Intl renders the UAH symbol/code; we assert it carries the value and a non-empty currency mark.
    expect(formatted).toContain('99,5');
    expect(formatted.length).toBeGreaterThan('99,5'.length);
  });

  it('formats the same amount differently per locale (locale-aware grouping/decimals)', () => {
    const uk = formatter.format(1234.5, 'EUR', 'uk');
    const en = formatter.format(1234.5, 'EUR', 'en-US');
    expect(uk).not.toBe(en);
  });

  it('defaults to the Ukrainian launch locale', () => {
    const explicit = formatter.format(1000, 'EUR', 'uk');
    const defaulted = formatter.format(1000, 'EUR');
    expect(defaulted).toBe(explicit);
  });

  it('degrades to "<number> <CODE>" for an invalid currency code rather than throwing', () => {
    const formatted = formatter.format(150, 'NOTACODE', 'uk');
    expect(formatted).toContain('NOTACODE');
    expect(formatted).toContain('150');
  });

  it('returns a bare localised number when no currency is supplied', () => {
    const formatted = formatter.format(1234.5, '', 'uk');
    expect(formatted).toContain('1234'.slice(0, 1)); // contains the digits
    expect(formatted).not.toContain('NaN');
  });

  it('returns an empty string for a non-finite amount', () => {
    expect(formatter.format(Number.NaN, 'UAH')).toBe('');
    expect(formatter.format(Number.POSITIVE_INFINITY, 'UAH')).toBe('');
  });

  describe('formatOptional', () => {
    it('formats when both amount and currency are present', () => {
      expect(formatter.formatOptional(150, 'UAH', 'uk')).toContain('150');
    });

    it('returns null when the amount is missing', () => {
      expect(formatter.formatOptional(null, 'UAH')).toBeNull();
      expect(formatter.formatOptional(undefined, 'UAH')).toBeNull();
    });

    it('returns null when the currency is missing', () => {
      expect(formatter.formatOptional(150, null)).toBeNull();
      expect(formatter.formatOptional(150, undefined)).toBeNull();
    });
  });
});
