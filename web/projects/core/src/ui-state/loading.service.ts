/**
 * Loading / async-state cross-cutting primitive.
 *
 * A tiny, framework-idiomatic (signals) async-state container the features reuse so every
 * spinner / error / empty-state is driven the same way (DRY). A {@link RequestState} factory holds
 * one request's lifecycle as readable signals (`status`, `data`, `error`) plus `loading`/`loaded`/
 * `failed`/`empty` computed views, and a `run(source$)` mutator that flips to `loading`, subscribes,
 * and lands on `loaded` (with the value) or `error` (with the typed {@link ApiError} the
 * error-normalization interceptor produced — never a raw `HttpErrorResponse`).
 *
 * It is deliberately view-independent: it touches no DOM and no Angular component, so the
 * presentational `loading`/`error-state` components in the `app` layer (and a React Native rewrite)
 * consume the same shape. The empty-state is data-driven via an optional `isEmpty` predicate so a
 * feature can say "loaded, but the list is empty" without re-implementing the pattern.
 */
import { DestroyRef, Injectable, computed, inject, signal, type Signal } from '@angular/core';
import { Subscription, type Observable } from 'rxjs';

import { isApiError, type ApiError } from '../domain/api-error';

/** The lifecycle of a single request. */
export type RequestStatus = 'idle' | 'loading' | 'loaded' | 'error';

/** Options for a {@link RequestState}. */
export interface RequestStateOptions<T> {
  /**
   * Optional predicate deciding whether a successfully-loaded value is "empty" (e.g. an empty list),
   * which drives the shared empty-state UI. Defaults to treating `null`/`undefined`/`[]` as empty.
   */
  readonly isEmpty?: (value: T) => boolean;
}

function defaultIsEmpty<T>(value: T): boolean {
  if (value === null || value === undefined) {
    return true;
  }
  return Array.isArray(value) && value.length === 0;
}

/**
 * Wrap a non-{@link ApiError} thrown value (a non-HTTP source's raw error) into the typed model so
 * `error` always exposes the one shape feature code reads. HTTP failures already arrive as an
 * `ApiError` via the error-normalization interceptor and never reach here.
 */
function asApiError(error: unknown): ApiError {
  const message =
    error instanceof Error ? error.message : typeof error === 'string' ? error : 'Unknown error';
  return {
    kind: 'unknown',
    status: 0,
    code: 'Client.Unknown',
    message,
    messageKey: 'errors.unknown',
  };
}

/**
 * One request's reactive async-state. Created via {@link LoadingService.create}; exposes readable
 * signals only (no public setters on the raw signals — mutation goes through `run`/`set`/`reset`).
 */
export class RequestState<T> {
  private readonly statusSignal = signal<RequestStatus>('idle');
  private readonly dataSignal = signal<T | undefined>(undefined);
  private readonly errorSignal = signal<ApiError | undefined>(undefined);
  private readonly isEmptyValue: (value: T) => boolean;
  private inFlight: Subscription | undefined;

  constructor(options?: RequestStateOptions<T>) {
    this.isEmptyValue = options?.isEmpty ?? defaultIsEmpty;
  }

  /** The raw lifecycle status. */
  readonly status: Signal<RequestStatus> = this.statusSignal.asReadonly();
  /** The loaded value, or `undefined` until a successful load. */
  readonly data: Signal<T | undefined> = this.dataSignal.asReadonly();
  /** The typed error from the last failed load, or `undefined`. */
  readonly error: Signal<ApiError | undefined> = this.errorSignal.asReadonly();

  /** True while a request is in flight. */
  readonly loading: Signal<boolean> = computed(() => this.statusSignal() === 'loading');
  /** True once a request has loaded successfully. */
  readonly loaded: Signal<boolean> = computed(() => this.statusSignal() === 'loaded');
  /** True when the last request failed. */
  readonly failed: Signal<boolean> = computed(() => this.statusSignal() === 'error');
  /** True when loaded successfully but the value is empty (drives the shared empty-state UI). */
  readonly empty: Signal<boolean> = computed(() => {
    if (this.statusSignal() !== 'loaded') {
      return false;
    }
    const value = this.dataSignal();
    return value === undefined ? true : this.isEmptyValue(value);
  });

  /**
   * Drive the state from an observable source: flip to `loading`, then land on `loaded` (value) or
   * `error` (typed {@link ApiError}). A previous in-flight subscription is cancelled first so a
   * re-run never lands a stale result. If the source completes without ever emitting (an `EMPTY`,
   * a fully-filtered stream, or a store test double) the state lands on `loaded` with `data`
   * `undefined`, which the `empty` computed surfaces — it must never stay stuck on `loading`.
   *
   * The error callback is typed `unknown` because RxJS error notifications carry any value: the
   * error-normalization interceptor guarantees an HTTP failure arrives as an {@link ApiError}, but a
   * non-HTTP source can throw something else, so the value is narrowed before it is stored.
   */
  run(source$: Observable<T>): void {
    this.inFlight?.unsubscribe();
    this.statusSignal.set('loading');
    this.errorSignal.set(undefined);
    this.inFlight = source$.subscribe({
      next: (value) => {
        this.dataSignal.set(value);
        this.statusSignal.set('loaded');
      },
      error: (error: unknown) => {
        this.errorSignal.set(isApiError(error) ? error : asApiError(error));
        this.statusSignal.set('error');
      },
      complete: () => {
        // Source completed without emitting: settle on `loaded` so the empty-state can fire rather
        // than leaving the UI stuck on a spinner.
        if (this.statusSignal() === 'loading') {
          this.dataSignal.set(undefined);
          this.statusSignal.set('loaded');
        }
      },
    });
  }

  /** Set a loaded value directly (e.g. from a cache hit) without an observable round-trip. */
  set(value: T): void {
    this.inFlight?.unsubscribe();
    this.inFlight = undefined;
    this.errorSignal.set(undefined);
    this.dataSignal.set(value);
    this.statusSignal.set('loaded');
  }

  /** Reset back to the initial idle state and cancel any in-flight request. */
  reset(): void {
    this.inFlight?.unsubscribe();
    this.inFlight = undefined;
    this.statusSignal.set('idle');
    this.dataSignal.set(undefined);
    this.errorSignal.set(undefined);
  }

  /**
   * Tear down the in-flight subscription. When the state was created via {@link LoadingService.create}
   * from an injection context, this is wired to the ambient {@link DestroyRef} and runs automatically
   * on owner destroy — owners do **not** need an `ngOnDestroy` purely to call it. Only a state created
   * **outside** an injection context (a manual factory call) must have `destroy()` called explicitly.
   */
  destroy(): void {
    this.inFlight?.unsubscribe();
    this.inFlight = undefined;
  }
}

/**
 * Factory for {@link RequestState} instances. Injectable so features get the shared async-state
 * pattern through DI; it holds no global state itself — each `create` returns an independent
 * request-scoped container.
 */
@Injectable({ providedIn: 'root' })
export class LoadingService {
  /**
   * Create a fresh request-scoped async-state container.
   *
   * When called inside an injection context (the common case — a component/service field
   * initialiser), the returned {@link RequestState} is **automatically torn down** when the owning
   * injector is destroyed: the in-flight subscription is cancelled via the ambient {@link DestroyRef}
   * so a request started just before the owner is destroyed cannot leak. Owners therefore do **not**
   * need to implement `ngOnDestroy` purely to call `state.destroy()`.
   *
   * If called outside an injection context (no ambient `DestroyRef`), no auto-teardown is wired and
   * the caller remains responsible for calling {@link RequestState.destroy} itself.
   */
  create<T>(options?: RequestStateOptions<T>): RequestState<T> {
    const state = new RequestState<T>(options);
    // `inject()` throws (NG0203) when there is **no** ambient injection context at all — the
    // `optional` flag only suppresses the not-found case, not the no-context case. Outside an
    // injection context (e.g. a direct `new LoadingService().create()` in a unit test, or a manual
    // factory call) auto-teardown simply does not apply and the caller owns `state.destroy()`.
    const destroyRef = this.resolveDestroyRef();
    destroyRef?.onDestroy(() => state.destroy());
    return state;
  }

  /**
   * Resolve the ambient {@link DestroyRef} when called from an injection context, or `undefined`
   * when there is no context to attach teardown to (so `create()` works both in and out of DI).
   */
  private resolveDestroyRef(): DestroyRef | undefined {
    try {
      return inject(DestroyRef, { optional: true }) ?? undefined;
    } catch {
      return undefined;
    }
  }
}
