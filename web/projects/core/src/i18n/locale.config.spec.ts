import {
  DEFAULT_LOCALE,
  LOCALE_STORAGE_KEY,
  SUPPORTED_LOCALES,
  SUPPORTED_LOCALE_CODES,
  asSupportedLocale,
} from './locale.config';

describe('locale.config (Ukrainian-first, additive locales)', () => {
  it('uses uk as the launch default locale', () => {
    expect(DEFAULT_LOCALE).toBe('uk');
  });

  it('lists uk first (populated) and includes the en additive-proof placeholder', () => {
    expect(SUPPORTED_LOCALES[0].code).toBe('uk');
    expect(SUPPORTED_LOCALE_CODES).toContain('en');
    // Every descriptor carries a non-empty autonym label (shown untranslated in the switch).
    for (const locale of SUPPORTED_LOCALES) {
      expect(locale.label.trim().length).toBeGreaterThan(0);
    }
  });

  it('persists the choice under a stable storage key', () => {
    expect(LOCALE_STORAGE_KEY.length).toBeGreaterThan(0);
  });

  describe('asSupportedLocale', () => {
    it('returns the code for a supported locale', () => {
      expect(asSupportedLocale('uk')).toBe('uk');
      expect(asSupportedLocale('en')).toBe('en');
    });

    it('returns undefined for an unsupported / empty / nullish code', () => {
      expect(asSupportedLocale('fr')).toBeUndefined();
      expect(asSupportedLocale('')).toBeUndefined();
      expect(asSupportedLocale(null)).toBeUndefined();
      expect(asSupportedLocale(undefined)).toBeUndefined();
    });
  });
});
