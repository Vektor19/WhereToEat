import { SESSION_ID_STORAGE_KEY, SessionIdProvider } from './session-id.provider';

describe('SessionIdProvider', () => {
  let provider: SessionIdProvider;

  beforeEach(() => {
    sessionStorage.clear();
    provider = new SessionIdProvider();
  });

  afterEach(() => sessionStorage.clear());

  it('returns a stable id within a session (repeat calls give the same id)', () => {
    const first = provider.getSessionId();
    const second = provider.getSessionId();
    expect(first).toBe(second);
    expect(first.length).toBeGreaterThan(0);
  });

  it('persists the id to sessionStorage so a new provider instance reuses it', () => {
    const id = provider.getSessionId();
    expect(sessionStorage.getItem(SESSION_ID_STORAGE_KEY)).toBe(id);

    // A fresh instance (same session/storage) returns the persisted id.
    const sameSession = new SessionIdProvider();
    expect(sameSession.getSessionId()).toBe(id);
  });

  it('mints a different id after reset() (new session)', () => {
    const first = provider.getSessionId();
    provider.reset();
    expect(sessionStorage.getItem(SESSION_ID_STORAGE_KEY)).toBeNull();
    const second = provider.getSessionId();
    expect(second).not.toBe(first);
  });

  it('produces an opaque id with no PII (a uuid-like / random token, not an email or name)', () => {
    const id = provider.getSessionId();
    expect(id).not.toContain('@');
    // No whitespace, reasonable opaque length.
    expect(id).not.toMatch(/\s/);
    expect(id.length).toBeGreaterThanOrEqual(10);
  });

  it('stays stable in-memory even when sessionStorage is unavailable', () => {
    const original = Object.getOwnPropertyDescriptor(globalThis, 'sessionStorage');
    Object.defineProperty(globalThis, 'sessionStorage', {
      configurable: true,
      get() {
        throw new Error('sessionStorage blocked');
      },
    });
    try {
      const fallbackProvider = new SessionIdProvider();
      const a = fallbackProvider.getSessionId();
      const b = fallbackProvider.getSessionId();
      expect(a).toBe(b);
    } finally {
      if (original) {
        Object.defineProperty(globalThis, 'sessionStorage', original);
      }
    }
  });
});
