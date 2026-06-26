import { Component, inject } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { EMPTY, Subject, throwError } from 'rxjs';

import type { ApiError } from '../domain/api-error';

import { LoadingService, RequestState } from './loading.service';

function apiError(): ApiError {
  return {
    kind: 'server',
    status: 500,
    code: 'Http.ServerError',
    message: 'boom',
    messageKey: 'errors.server',
  };
}

describe('LoadingService / RequestState', () => {
  let service: LoadingService;

  beforeEach(() => {
    service = new LoadingService();
  });

  it('starts idle with no data and no error', () => {
    const state = service.create<number>();
    expect(state.status()).toBe('idle');
    expect(state.loading()).toBe(false);
    expect(state.loaded()).toBe(false);
    expect(state.failed()).toBe(false);
    expect(state.data()).toBeUndefined();
    expect(state.error()).toBeUndefined();
  });

  it('transitions idle → loading → loaded with the value on success', () => {
    const state = service.create<number>();
    const source = new Subject<number>();

    state.run(source.asObservable());
    expect(state.loading()).toBe(true);
    expect(state.status()).toBe('loading');

    source.next(42);
    source.complete();
    expect(state.loaded()).toBe(true);
    expect(state.data()).toBe(42);
    expect(state.failed()).toBe(false);
  });

  it('transitions to error with the typed ApiError on failure', () => {
    const state = service.create<number>();
    const source = new Subject<number>();
    const err = apiError();

    state.run(source.asObservable());
    source.error(err);

    expect(state.failed()).toBe(true);
    expect(state.status()).toBe('error');
    expect(state.error()).toEqual(err);
    expect(state.loading()).toBe(false);
  });

  it('run() lands on loaded/empty when the source completes without emitting', () => {
    const state = service.create<number[]>();

    state.run(EMPTY);

    expect(state.loading()).toBe(false);
    expect(state.status()).toBe('loaded');
    expect(state.data()).toBeUndefined();
    expect(state.empty()).toBe(true);
    expect(state.failed()).toBe(false);
  });

  it('run() does not overwrite an emitted value when the source later completes', () => {
    const state = service.create<number>();
    const source = new Subject<number>();
    state.run(source.asObservable());
    source.next(5);
    source.complete();
    expect(state.status()).toBe('loaded');
    expect(state.data()).toBe(5);
  });

  it('run() wraps a non-ApiError thrown value into a typed unknown ApiError', () => {
    const state = service.create<number>();

    state.run(throwError(() => 'plain string failure'));

    expect(state.failed()).toBe(true);
    const error = state.error();
    expect(error?.kind).toBe('unknown');
    expect(error?.message).toBe('plain string failure');
    expect(error?.messageKey).toBe('errors.unknown');
  });

  it('destroy() cancels an in-flight request so a late emission cannot land', () => {
    const state = service.create<number>();
    const source = new Subject<number>();
    state.run(source.asObservable());

    state.destroy();
    source.next(99);

    expect(state.data()).toBeUndefined();
    expect(state.status()).toBe('loading');
  });

  it('clears a previous error when a new run starts', () => {
    const state = service.create<number>();
    const first = new Subject<number>();
    state.run(first.asObservable());
    first.error(apiError());
    expect(state.error()).toBeDefined();

    const second = new Subject<number>();
    state.run(second.asObservable());
    expect(state.error()).toBeUndefined();
    expect(state.loading()).toBe(true);
  });

  it('cancels an in-flight request when re-run so a stale result cannot land', () => {
    const state = service.create<number>();
    const stale = new Subject<number>();
    state.run(stale.asObservable());

    const fresh = new Subject<number>();
    state.run(fresh.asObservable());

    // The first (cancelled) source emits — it must be ignored.
    stale.next(1);
    expect(state.data()).toBeUndefined();

    fresh.next(2);
    expect(state.data()).toBe(2);
  });

  it('flags an empty result with the default empty predicate (empty array / null)', () => {
    const state = service.create<number[]>();
    const source = new Subject<number[]>();
    state.run(source.asObservable());
    source.next([]);
    expect(state.loaded()).toBe(true);
    expect(state.empty()).toBe(true);
  });

  it('does not flag a non-empty result as empty', () => {
    const state = service.create<number[]>();
    const source = new Subject<number[]>();
    state.run(source.asObservable());
    source.next([1]);
    expect(state.empty()).toBe(false);
  });

  it('honours a custom isEmpty predicate', () => {
    const state = service.create<{ total: number }>({ isEmpty: (v) => v.total === 0 });
    const source = new Subject<{ total: number }>();
    state.run(source.asObservable());
    source.next({ total: 0 });
    expect(state.empty()).toBe(true);
  });

  it('set() lands a loaded value directly without an observable', () => {
    const state = service.create<string>();
    state.set('cached');
    expect(state.loaded()).toBe(true);
    expect(state.data()).toBe('cached');
  });

  it('reset() returns to idle and clears data/error', () => {
    const state = service.create<number>();
    state.set(7);
    state.reset();
    expect(state.status()).toBe('idle');
    expect(state.data()).toBeUndefined();
    expect(state.error()).toBeUndefined();
  });

  it('create() returns independent states', () => {
    const a = service.create<number>();
    const b = service.create<number>();
    a.set(1);
    expect(b.status()).toBe('idle');
    expect(a).toBeInstanceOf(RequestState);
  });

  it('auto-tears-down a state created in an injection context when the owner is destroyed', () => {
    const source = new Subject<number>();
    let owned: RequestState<number> | undefined;

    @Component({ selector: 'lib-loading-host', template: '' })
    class HostComponent {
      readonly state = inject(LoadingService).create<number>();
      constructor() {
        owned = this.state;
      }
    }

    const fixture = TestBed.createComponent(HostComponent);
    fixture.componentInstance.state.run(source.asObservable());
    expect(owned?.status()).toBe('loading');

    // Destroying the component must cancel the in-flight request via the ambient DestroyRef,
    // so a late emission can no longer land (no leaked subscription).
    fixture.destroy();
    source.next(7);

    expect(owned?.data()).toBeUndefined();
    expect(owned?.status()).toBe('loading');
  });

  it('keeps two concurrent owners isolated: destroying one tears down only its own state', () => {
    // The singleton `LoadingService` resolves the ambient `DestroyRef` per `create()` call, so each
    // owner's teardown must be wired to *that* owner's injector — destroying one must not cancel the
    // other's in-flight request. This guards that per-instance `DestroyRef` isolation.
    const owned: RequestState<number>[] = [];

    @Component({ selector: 'lib-loading-host-iso', template: '' })
    class HostComponent {
      readonly state = inject(LoadingService).create<number>();
      constructor() {
        owned.push(this.state);
      }
    }

    const first = TestBed.createComponent(HostComponent);
    const second = TestBed.createComponent(HostComponent);
    const [firstState, secondState] = owned;

    const firstSource = new Subject<number>();
    const secondSource = new Subject<number>();
    firstState.run(firstSource.asObservable());
    secondState.run(secondSource.asObservable());

    // Destroy only the first owner: its in-flight request must be cancelled.
    first.destroy();
    firstSource.next(1);
    expect(firstState.data()).toBeUndefined();
    expect(firstState.status()).toBe('loading');

    // The second owner is still alive: its emission must still land.
    secondSource.next(2);
    expect(secondState.data()).toBe(2);
    expect(secondState.status()).toBe('loaded');

    second.destroy();
  });
});
