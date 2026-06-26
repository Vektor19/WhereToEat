/**
 * Analytics session-id provider.
 *
 * Supplies a **per-session, opaque** client correlation id that the analytics event builders
 * (Step 8) stamp onto every `RawEventBody` as `sessionId`. The id is:
 *   - **opaque** — a random UUID with no PII, no user identity, nothing derivable about the person;
 *   - **stable within a session** — persisted in `sessionStorage`, so every event in the same
 *     browser-tab session shares one id;
 *   - **fresh across sessions** — `sessionStorage` is cleared when the tab/session ends, so the next
 *     session mints a new id.
 *
 * The backend anonymizes server-side (invariant #11) — this id is only a client-side batching
 * correlation handle, never an identifier of the user. The provider degrades gracefully where
 * `sessionStorage` is unavailable (e.g. private-mode quirks, SSR): it keeps an in-memory id so the
 * id is still stable for the lifetime of the running app.
 */
import { Injectable } from '@angular/core';

/** The `sessionStorage` key under which the opaque session id is persisted. */
export const SESSION_ID_STORAGE_KEY = 'dp.analytics.sessionId';

@Injectable({ providedIn: 'root' })
export class SessionIdProvider {
  /** In-memory fallback id, used when `sessionStorage` is unavailable. */
  private memoryId: string | undefined;

  /**
   * Return the current session's opaque id, minting and persisting one on first access. The same id
   * is returned for every subsequent call within the session.
   */
  getSessionId(): string {
    const stored = this.readStored();
    if (stored !== null && stored.length > 0) {
      this.memoryId = stored;
      return stored;
    }

    const id = this.mintId();
    this.memoryId = id;
    this.writeStored(id);
    return id;
  }

  /**
   * Discard the current session id so the next {@link getSessionId} mints a fresh one. Exposed for
   * tests (simulating a new session) and for an explicit "new session" reset if a feature ever needs
   * one; not part of the normal flow.
   */
  reset(): void {
    this.memoryId = undefined;
    this.removeStored();
  }

  /** Mint a new opaque id. Uses `crypto.randomUUID` when present, with a non-crypto fallback. */
  private mintId(): string {
    const cryptoObj = globalThis.crypto;
    if (cryptoObj && typeof cryptoObj.randomUUID === 'function') {
      return cryptoObj.randomUUID();
    }
    // Fallback for environments without `crypto.randomUUID`: still opaque, no PII.
    return `${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 12)}`;
  }

  private storage(): Storage | null {
    try {
      return globalThis.sessionStorage ?? null;
    } catch {
      // Accessing `sessionStorage` can throw in sandboxed contexts.
      return null;
    }
  }

  private readStored(): string | null {
    const store = this.storage();
    if (store === null) {
      return this.memoryId ?? null;
    }
    try {
      return store.getItem(SESSION_ID_STORAGE_KEY);
    } catch {
      return this.memoryId ?? null;
    }
  }

  private writeStored(id: string): void {
    const store = this.storage();
    if (store === null) {
      return;
    }
    try {
      store.setItem(SESSION_ID_STORAGE_KEY, id);
    } catch {
      // Persisting failed (quota / private mode): the in-memory id keeps it stable for this run.
    }
  }

  private removeStored(): void {
    const store = this.storage();
    if (store === null) {
      return;
    }
    try {
      store.removeItem(SESSION_ID_STORAGE_KEY);
    } catch {
      // Ignore — `reset` is best-effort.
    }
  }
}
