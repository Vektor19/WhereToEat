/**
 * Geo opt-in store (Step 15) — the persisted record of whether the user has opted in to approximate
 * distance ranking (invariant #11). It owns **only the choice**, not the coordinates: the approximate
 * `userGeo` lives in the {@link SelectionStore} (and is re-acquired on opt-in), so we never persist a
 * stale position — only the boolean preference crosses sessions.
 *
 * Default is **OFF** — distance ranking is on only after an explicit opt-in (invariant #11). The
 * choice is persisted in `localStorage` so it survives across sessions; the store degrades to an
 * in-memory value where `localStorage` is unavailable (private mode / SSR). It exposes a readable
 * signal plus explicit mutators, mirroring the signal-store ergonomics without needing the request
 * machinery of the {@link SignalStore} base (this state has no derived request shape).
 *
 * View-independent (no DOM) so it mirrors 1:1 onto an RN container (where the persistence seam swaps
 * to `AsyncStorage`).
 */
import { Injectable, signal, type Signal } from '@angular/core';

/** The `localStorage` key under which the opt-in choice is persisted across sessions. */
export const GEO_OPTIN_STORAGE_KEY = 'dp.geo.optIn';

@Injectable({ providedIn: 'root' })
export class GeoOptInStore {
  /** The persisted opt-in choice; seeded from storage so the choice survives a reload. */
  private readonly optedInState = signal<boolean>(this.readStored());

  /** True when the user has opted in to approximate distance ranking. Default OFF. */
  readonly optedIn: Signal<boolean> = this.optedInState.asReadonly();

  /** Record an opt-in (persisted) — the UI calls this only after acquiring approximate coordinates. */
  optIn(): void {
    this.set(true);
  }

  /** Record an opt-out (persisted) — the UI calls this when the user disables distance ranking. */
  optOut(): void {
    this.set(false);
  }

  private set(value: boolean): void {
    this.optedInState.set(value);
    this.writeStored(value);
  }

  private storage(): Storage | null {
    try {
      return globalThis.localStorage ?? null;
    } catch {
      return null;
    }
  }

  private readStored(): boolean {
    const store = this.storage();
    if (store === null) {
      return false;
    }
    try {
      return store.getItem(GEO_OPTIN_STORAGE_KEY) === 'true';
    } catch {
      return false;
    }
  }

  private writeStored(value: boolean): void {
    const store = this.storage();
    if (store === null) {
      return;
    }
    try {
      if (value) {
        store.setItem(GEO_OPTIN_STORAGE_KEY, 'true');
      } else {
        // Opt-out removes the key entirely (privacy-clean): absence unambiguously means "not opted in".
        store.removeItem(GEO_OPTIN_STORAGE_KEY);
      }
    } catch {
      // Persisting failed (quota / private mode): the in-memory signal keeps it for this run.
    }
  }
}
