/**
 * Distance formatter (Step 8). Presents a distance **in kilometres** — the unit invariant #7/#11
 * and design Goal 4 mandate ("X km from you"). The recommend DTO already carries `distanceKm` when
 * the user opted into geo; this service localises that number for display (it does **not** compute
 * distance — {@link Haversine} does, for the rare client-side display case).
 *
 * Locale-aware via `Intl.NumberFormat`: Ukrainian (`uk`) is the launch default and the locale is a
 * parameter so Step 17 drives it from the active i18n locale. The "km" suffix is rendered here as a
 * literal unit symbol via `Intl`'s `unit` style, so it stays consistent and Step 17 does not need a
 * separate copy key for it; no other copy is baked in.
 *
 * Framework-light: an injectable `Intl` wrapper mirrored 1:1 by the RN rewrite.
 */
import { Injectable } from '@angular/core';

import { DEFAULT_LOCALE } from './currency.formatter';

@Injectable({ providedIn: 'root' })
export class DistanceFormatter {
  /**
   * Format `distanceKm` as a localised "<value> km" string with at most one fractional digit
   * (e.g. `1.2 km`, `350 m`-equivalents are intentionally **not** introduced — the product shows km).
   * Sub-kilometre distances keep one decimal (e.g. `0.3 km`) so they are not rounded to `0 km`.
   */
  formatKm(distanceKm: number, locale: string = DEFAULT_LOCALE): string {
    if (!Number.isFinite(distanceKm) || distanceKm < 0) {
      return '';
    }

    try {
      return new Intl.NumberFormat(locale, {
        style: 'unit',
        unit: 'kilometer',
        unitDisplay: 'short',
        maximumFractionDigits: 1,
      }).format(distanceKm);
    } catch {
      // Defensive: older Intl without unit support degrades to "<number> km".
      const value = new Intl.NumberFormat(locale, { maximumFractionDigits: 1 }).format(distanceKm);
      return `${value} km`;
    }
  }

  /**
   * Format a distance that may be absent (the recommend DTO's `distanceKm` is null/absent when the
   * user did not opt into geo — invariant #11). Returns `null` so the card renders no distance at
   * all (never "0 km") when geo was off.
   */
  formatOptionalKm(
    distanceKm: number | null | undefined,
    locale: string = DEFAULT_LOCALE,
  ): string | null {
    if (distanceKm === null || distanceKm === undefined) {
      return null;
    }
    return this.formatKm(distanceKm, locale);
  }
}
