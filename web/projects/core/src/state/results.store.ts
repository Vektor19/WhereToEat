/**
 * Results store — holds the last recommendation result and the request that produced it, driven by
 * submitting a built {@link RecommendationRequest} through the `recommend` data-access port (Step 5).
 *
 * The result is rendered **in backend order**: the engine has already ranked and filtered the list
 * (explicit sort dominates; coverage is only a tie-breaker — invariant #5), so the store preserves
 * the order it receives and never re-sorts client-side. It exposes the ordered `restaurants` plus
 * the loading/error/empty sub-state through the shared {@link RequestState} primitive (Step 7) so the
 * UI lifecycle is consistent, and it echoes the submitted `request` so the results view can show
 * "what produced this" (e.g. the chosen sort/match) without re-reading the live selection, which may
 * have changed since submit.
 *
 * A new submit clears the previous result (flips to loading) so a stale list never lingers.
 * View-independent (no DOM) so it mirrors 1:1 onto an RN container.
 *
 * Like every store in the app it extends {@link SignalStore} (Step 9 preamble — the base is applied
 * consistently): the request's {@link RequestState} lifecycle and the echoed request live as members
 * of the base-managed immutable state object, so the store has the same 1:1 RN-blueprint shape as the
 * others and there are no public setters on a raw signal.
 */
import { Injectable, inject, type Signal } from '@angular/core';

import type {
  RecommendationRequest,
  RecommendationResultDto,
  RecommendedRestaurantDto,
} from '../domain';
import type { ApiError } from '../domain/api-error';
import { RECOMMEND, type RecommendOperation } from '../data-access';
import { LoadingService, type RequestState } from '../ui-state/loading.service';
import { SignalStore } from './signal-store.base';

/**
 * The immutable results state the store owns. The {@link RequestState} reference is stable (created
 * once in the constructor); only the echoed `request` changes via the base's `patchState`, so the
 * state object stays a transparent 1:1 RN-blueprint slice.
 */
interface ResultsState {
  /** The recommend request lifecycle (its own signals drive the loading/error/empty sub-state). */
  readonly result: RequestState<RecommendationResultDto>;
  /** The echoed request that produced the current result, or `undefined` before the first submit. */
  readonly request: RecommendationRequest | undefined;
}

@Injectable({ providedIn: 'root' })
export class ResultsStore extends SignalStore<ResultsState> {
  private readonly recommend: RecommendOperation;
  /** The request lifecycle held in state — the value is "empty" when the engine returned no rows. */
  private readonly resultState: RequestState<RecommendationResultDto>;

  constructor() {
    const recommend = inject(RECOMMEND);
    const resultState = inject(LoadingService).create<RecommendationResultDto>({
      isEmpty: (result) => result.restaurants.length === 0,
    });
    super({ result: resultState, request: undefined });
    this.recommend = recommend;
    this.resultState = resultState;
  }

  /** The full recommendation result, or `undefined` until the first successful submit. */
  readonly result: Signal<RecommendationResultDto | undefined> = this.select((s) =>
    s.result.data(),
  );

  /**
   * The ranked restaurants in **backend order** (never re-sorted client-side — invariant #5), or an
   * empty list before a result has loaded.
   */
  readonly restaurants: Signal<readonly RecommendedRestaurantDto[]> = this.select(
    (s) => s.result.data()?.restaurants ?? [],
  );

  /** True while a recommend request is in flight. */
  readonly loadingResults: Signal<boolean> = this.select((s) => s.result.loading());
  /** True once a result has loaded successfully. */
  readonly loaded: Signal<boolean> = this.select((s) => s.result.loaded());
  /** The typed error from the last failed submit, or `undefined`. */
  readonly error: Signal<ApiError | undefined> = this.select((s) => s.result.error());
  /** True when a result loaded successfully but the engine returned no restaurants. */
  readonly empty: Signal<boolean> = this.select((s) => s.result.empty());

  /** The request that produced the current (or in-flight) result, or `undefined` before any submit. */
  readonly request: Signal<RecommendationRequest | undefined> = this.select((s) => s.request);

  /**
   * Submit a built recommendation request: clears the previous result, flips to loading, and lands
   * on loaded (the ordered result) or error (the typed {@link ApiError}). The request is echoed so
   * {@link request} reflects what produced the current/loading result. A re-submit cancels any
   * in-flight request so a stale result cannot land (handled by {@link RequestState}).
   */
  submit(request: RecommendationRequest): void {
    this.patchState({ request });
    this.resultState.run(this.recommend.execute(request));
  }

  /** Clear the result and echoed request back to the initial idle state. */
  clear(): void {
    this.patchState({ request: undefined });
    this.resultState.reset();
  }
}
