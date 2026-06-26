import { type Signal } from '@angular/core';

import { SignalStore } from './signal-store.base';

interface CounterState {
  readonly count: number;
  readonly label: string;
}

/** A minimal concrete store to exercise the protected base API. */
class CounterStore extends SignalStore<CounterState> {
  constructor() {
    super({ count: 0, label: 'init' });
  }

  readonly count: Signal<number> = this.select((s) => s.count);

  increment(): void {
    this.patchState((s) => ({ count: s.count + 1 }));
  }

  setLabel(label: string): void {
    this.patchState({ label });
  }

  replace(next: CounterState): void {
    this.setState(next);
  }
}

describe('SignalStore (base)', () => {
  let store: CounterStore;

  beforeEach(() => {
    store = new CounterStore();
  });

  it('exposes the initial state as a readable signal', () => {
    expect(store.state()).toEqual({ count: 0, label: 'init' });
    expect(store.count()).toBe(0);
  });

  it('patchState with an updater computes from the previous state immutably', () => {
    const before = store.state();
    store.increment();
    expect(store.count()).toBe(1);
    // The previous state object is not mutated (new reference).
    expect(before).toEqual({ count: 0, label: 'init' });
    expect(store.state()).not.toBe(before);
  });

  it('patchState with a partial merges and leaves other fields intact', () => {
    store.setLabel('changed');
    expect(store.state()).toEqual({ count: 0, label: 'changed' });
  });

  it('setState replaces the whole state object', () => {
    store.replace({ count: 9, label: 'replaced' });
    expect(store.state()).toEqual({ count: 9, label: 'replaced' });
  });

  it('derived select signals track state changes', () => {
    expect(store.count()).toBe(0);
    store.increment();
    store.increment();
    expect(store.count()).toBe(2);
  });
});
