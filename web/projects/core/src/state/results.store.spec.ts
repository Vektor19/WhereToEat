import { TestBed } from '@angular/core/testing';
import { Subject, of, throwError } from 'rxjs';

import type {
  RecommendationRequest,
  RecommendationResultDto,
  RecommendedRestaurantDto,
} from '../domain';
import type { ApiError } from '../domain/api-error';
import { RECOMMEND, type RecommendOperation } from '../data-access';
import { ResultsStore } from './results.store';

const REQUEST: RecommendationRequest = {
  items: [{ categoryId: 'cat-1' }],
  match: 'or',
  sort: 'price',
  filters: [],
};

// Deliberately out of "price" order to prove the store does NOT re-sort (invariant #5).
const RANKED: readonly RecommendedRestaurantDto[] = [
  { restaurantId: 'r-3', name: 'Third', ratingCount: 10, coverage: 1 },
  { restaurantId: 'r-1', name: 'First', ratingCount: 5, coverage: 1 },
  { restaurantId: 'r-2', name: 'Second', ratingCount: 8, coverage: 1 },
];

function result(restaurants: readonly RecommendedRestaurantDto[]): RecommendationResultDto {
  return { match: 'or', sort: 'price', restaurants };
}

function apiError(): ApiError {
  return {
    kind: 'server',
    status: 500,
    code: 'Http.ServerError',
    message: 'boom',
    messageKey: 'errors.server',
  };
}

class FakeRecommend implements RecommendOperation {
  calls: RecommendationRequest[] = [];
  source = of(result(RANKED));
  execute(request: RecommendationRequest) {
    this.calls.push(request);
    return this.source;
  }
}

function setup(fake?: FakeRecommend): { store: ResultsStore; recommend: FakeRecommend } {
  const recommend = fake ?? new FakeRecommend();
  TestBed.configureTestingModule({
    providers: [ResultsStore, { provide: RECOMMEND, useValue: recommend }],
  });
  return { store: TestBed.inject(ResultsStore), recommend };
}

describe('ResultsStore', () => {
  describe('initial state', () => {
    it('is idle with no result, empty restaurants, no request echo', () => {
      const { store } = setup();
      expect(store.result()).toBeUndefined();
      expect(store.restaurants()).toEqual([]);
      expect(store.loadingResults()).toBe(false);
      expect(store.loaded()).toBe(false);
      expect(store.request()).toBeUndefined();
    });
  });

  describe('submit transitions', () => {
    it('idle → loading while in flight', () => {
      const fake = new FakeRecommend();
      fake.source = new Subject<RecommendationResultDto>();
      const { store } = setup(fake);
      store.submit(REQUEST);
      expect(store.loadingResults()).toBe(true);
      expect(store.loaded()).toBe(false);
    });

    it('loading → loaded with the result', () => {
      const { store } = setup();
      store.submit(REQUEST);
      expect(store.loaded()).toBe(true);
      expect(store.loadingResults()).toBe(false);
      expect(store.result()).toEqual(result(RANKED));
    });

    it('loading → error with the typed ApiError', () => {
      const fake = new FakeRecommend();
      fake.source = throwError(() => apiError());
      const { store } = setup(fake);
      store.submit(REQUEST);
      expect(store.error()?.code).toBe('Http.ServerError');
      expect(store.loaded()).toBe(false);
    });

    it('flags an empty result when the engine returns no restaurants', () => {
      const fake = new FakeRecommend();
      fake.source = of(result([]));
      const { store } = setup(fake);
      store.submit(REQUEST);
      expect(store.loaded()).toBe(true);
      expect(store.empty()).toBe(true);
      expect(store.restaurants()).toEqual([]);
    });
  });

  describe('backend order is preserved (invariant #5)', () => {
    it('exposes restaurants in the exact order received, never re-sorted', () => {
      const { store } = setup();
      store.submit(REQUEST);
      expect(store.restaurants().map((r) => r.restaurantId)).toEqual(['r-3', 'r-1', 'r-2']);
    });
  });

  describe('request echo', () => {
    it('echoes the submitted request', () => {
      const { store } = setup();
      store.submit(REQUEST);
      expect(store.request()).toEqual(REQUEST);
    });

    it('updates the echo to the latest submit', () => {
      const { store } = setup();
      store.submit(REQUEST);
      const second: RecommendationRequest = { ...REQUEST, sort: 'rating' };
      store.submit(second);
      expect(store.request()).toEqual(second);
    });
  });

  describe('re-submit and clear', () => {
    it('clears the previous result on a new submit (flips back to loading)', () => {
      const fake = new FakeRecommend();
      const { store } = setup(fake);
      store.submit(REQUEST);
      expect(store.loaded()).toBe(true);

      // Second submit with a never-completing source: must flip to loading, not keep the stale result.
      fake.source = new Subject<RecommendationResultDto>();
      store.submit(REQUEST);
      expect(store.loadingResults()).toBe(true);
      expect(store.loaded()).toBe(false);
    });

    it('a stale in-flight result cannot land after a re-submit', () => {
      const fake = new FakeRecommend();
      const stale = new Subject<RecommendationResultDto>();
      fake.source = stale;
      const { store } = setup(fake);
      store.submit(REQUEST);

      const fresh = new Subject<RecommendationResultDto>();
      fake.source = fresh;
      store.submit(REQUEST);

      stale.next(result([{ restaurantId: 'stale', name: 'Stale', ratingCount: 0, coverage: 1 }]));
      expect(store.result()).toBeUndefined();

      fresh.next(result(RANKED));
      expect(store.restaurants().map((r) => r.restaurantId)).toEqual(['r-3', 'r-1', 'r-2']);
    });

    it('clear resets the result and the request echo', () => {
      const { store } = setup();
      store.submit(REQUEST);
      store.clear();
      expect(store.result()).toBeUndefined();
      expect(store.restaurants()).toEqual([]);
      expect(store.request()).toBeUndefined();
      expect(store.loaded()).toBe(false);
    });
  });
});
