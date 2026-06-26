/**
 * Hand-rolled signal-store base — the cohesive, view-independent reactive state primitive every
 * store in the app extends (Step 9). It is deliberately the smallest possible thing that gives a
 * store the Angular-idiomatic shape the design chose (readable signals + explicit mutators) while
 * staying a transparent 1:1 blueprint for the React Native rewrite — no NgRx, no DOM, no component
 * coupling, just `signal`/`computed` over a single immutable state object.
 *
 * Why a base class instead of NgRx SignalStore (design Decision #2): keeping the store shape a plain
 * `signal<State>` with named mutator methods makes the transitions obvious and the RN container a
 * direct mirror, and avoids a heavy dependency for state this small. Every store applies it
 * consistently.
 *
 * The contract this base enforces:
 *  - **state is read-only from outside** — `state` is a readonly {@link Signal}; subclasses mutate
 *    only through the protected `setState`/`patchState` helpers, so there are no public setters on a
 *    raw writable signal (the Step 9 acceptance criterion).
 *  - **derive with `select`** — subclasses expose `computed` views over slices of state.
 *  - **immutability** — `patchState` produces a new state object; the writable signal's reference
 *    only ever changes through the helpers, so change detection and equality checks stay predictable.
 */
import { computed, signal, type Signal } from '@angular/core';

/**
 * Base class for a signal-backed store over an immutable `State` object.
 *
 * Subclasses pass the initial state to `super(...)`, read through {@link state} (or {@link select}),
 * and mutate through {@link setState} / {@link patchState}. The writable signal is private so the
 * only way to change state is a subclass mutator — that is what keeps the "no public setters on raw
 * signals" guarantee.
 */
export abstract class SignalStore<State extends object> {
  private readonly stateSignal;

  /** The full store state as a readable signal (no external mutation — use a subclass mutator). */
  readonly state: Signal<State>;

  protected constructor(initialState: State) {
    this.stateSignal = signal(initialState);
    this.state = this.stateSignal.asReadonly();
  }

  /**
   * Derive a memoised read-only view over a slice of state. Equivalent to `computed(() =>
   * selector(this.state()))`; provided so subclasses express derived signals uniformly.
   */
  protected select<Slice>(selector: (state: State) => Slice): Signal<Slice> {
    return computed(() => selector(this.stateSignal()));
  }

  /** Replace the whole state object (used when a mutation depends on the previous state). */
  protected setState(next: State): void {
    this.stateSignal.set(next);
  }

  /**
   * Merge a partial patch into the current state, producing a new immutable state object. The
   * updater form receives the current state so a mutation can be computed from it without a separate
   * read.
   */
  protected patchState(patch: Partial<State> | ((state: State) => Partial<State>)): void {
    this.stateSignal.update((current) => {
      const resolved = typeof patch === 'function' ? patch(current) : patch;
      return { ...current, ...resolved };
    });
  }
}
