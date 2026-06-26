/**
 * Currency formatter (Step 8). Formats a money amount **by the backend's `PriceCurrency`** — the
 * ISO 4217 code the per-menu-item / per-basket DTO carries (`basketPriceCurrency`, the menu item's
 * currency). The currency symbol/code is **never hardcoded** (no baked-in ₴): the UI formats by the
 * returned code so the same code path serves any currency the backend stores (invariant: Ukrainian-
 * first but currency-driven, design Goal 4 / §10).
 *
 * Locale-aware via the Intl API: Ukrainian (`uk`) is the launch default, but the locale is a
 * parameter so Step 17 can drive it from the active i18n locale. **No i18n catalog strings are
 * baked here** — this is pure number/currency formatting, which is exactly the locale-aware
 * formatting Step 17 wires through (no hardcoded copy that would block it).
 *
 * Framework-light: an injectable service wrapping `Intl.NumberFormat`, mirrored 1:1 by the RN
 * rewrite (which has the same `Intl` API).
 */
import { Injectable } from '@angular/core';

// The launch default locale lives in the i18n layer (single source of truth, Step 17). Re-exported
// here so the formatter modules keep their historical `DEFAULT_LOCALE` import path without defining a
// second constant (which would collide in the public-api barrel). Callers may override per active
// locale — the view layer passes `LocaleService.activeLocale()` so formatting follows the switch.
export { DEFAULT_LOCALE } from '../i18n/locale.config';
import { DEFAULT_LOCALE } from '../i18n/locale.config';

@Injectable({ providedIn: 'root' })
export class CurrencyFormatter {
  /**
   * Format `amount` in the currency identified by ISO 4217 `currencyCode` (e.g. `UAH`, `EUR`,
   * `USD`), localised for `locale` (defaults to {@link DEFAULT_LOCALE}). The symbol and grouping
   * come from `Intl`, driven entirely by the supplied code — never a hardcoded symbol.
   *
   * Falls back to a plain decimal + code (e.g. `150 XYZ`) when the code is malformed/unknown, so a
   * bad backend code degrades gracefully rather than throwing into the view.
   */
  format(amount: number, currencyCode: string, locale: string = DEFAULT_LOCALE): string {
    if (!Number.isFinite(amount)) {
      return '';
    }

    const code = (currencyCode ?? '').trim();
    if (code.length === 0) {
      // No currency supplied: return the bare localised number rather than inventing a symbol.
      return new Intl.NumberFormat(locale).format(amount);
    }

    try {
      return new Intl.NumberFormat(locale, {
        style: 'currency',
        currency: code,
      }).format(amount);
    } catch {
      // `Intl` throws `RangeError` on an invalid currency code: degrade to "<number> <CODE>".
      return `${new Intl.NumberFormat(locale).format(amount)} ${code}`;
    }
  }

  /**
   * Format a price that may be absent (the recommend DTO's `basketPriceAmount`/`basketPriceCurrency`
   * are optional/nullable — the engine omits them when it could not compute a basket). Returns
   * `null` when either the amount or the currency is missing, so the caller renders nothing (no
   * misleading "0" or stray symbol) rather than a broken price.
   */
  formatOptional(
    amount: number | null | undefined,
    currencyCode: string | null | undefined,
    locale: string = DEFAULT_LOCALE,
  ): string | null {
    if (amount === null || amount === undefined || !currencyCode) {
      return null;
    }
    return this.format(amount, currencyCode, locale);
  }
}
