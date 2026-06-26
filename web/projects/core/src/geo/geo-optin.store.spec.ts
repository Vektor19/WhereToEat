import { GEO_OPTIN_STORAGE_KEY, GeoOptInStore } from './geo-optin.store';

describe('GeoOptInStore', () => {
  beforeEach(() => localStorage.clear());
  afterEach(() => localStorage.clear());

  it('defaults to opted-out (distance ranking off until explicit opt-in — invariant #11)', () => {
    expect(new GeoOptInStore().optedIn()).toBe(false);
  });

  it('opts in and persists the choice across instances (sessions)', () => {
    const store = new GeoOptInStore();
    store.optIn();
    expect(store.optedIn()).toBe(true);
    expect(localStorage.getItem(GEO_OPTIN_STORAGE_KEY)).toBe('true');
    // A fresh instance (a later session) reads the persisted choice.
    expect(new GeoOptInStore().optedIn()).toBe(true);
  });

  it('opts out by removing the storage key (privacy-clean) and a fresh instance reads false', () => {
    const store = new GeoOptInStore();
    store.optIn();
    store.optOut();
    expect(store.optedIn()).toBe(false);
    // Opt-out removes the key entirely rather than storing a literal 'false'.
    expect(localStorage.getItem(GEO_OPTIN_STORAGE_KEY)).toBeNull();
    expect(new GeoOptInStore().optedIn()).toBe(false);
  });
});
