import { TestBed } from '@angular/core/testing';
import { Subject, of, throwError, type Observable } from 'rxjs';

import type { CategoryDto, DishDto } from '../domain';
import type { ApiError } from '../domain/api-error';
import {
  GET_CATEGORIES,
  GET_DISHES_BY_CATEGORY,
  type GetCategoriesOperation,
  type GetDishesByCategoryOperation,
} from '../data-access';
import { CatalogStore } from './catalog.store';

const CATEGORIES: readonly CategoryDto[] = [
  { id: 'cat-1', name: 'First courses' },
  { id: 'cat-2', name: 'Fast food' },
];

const DISHES_CAT_1: readonly DishDto[] = [
  { id: 'dish-1', categoryId: 'cat-1', canonicalName: 'Borscht' },
];

function apiError(): ApiError {
  return {
    kind: 'server',
    status: 500,
    code: 'Http.ServerError',
    message: 'boom',
    messageKey: 'errors.server',
  };
}

class FakeGetCategories implements GetCategoriesOperation {
  calls = 0;
  source = of(CATEGORIES);
  execute() {
    this.calls += 1;
    return this.source;
  }
}

class FakeGetDishes implements GetDishesByCategoryOperation {
  callsByCategory = new Map<string, number>();
  sourceByCategory = new Map<string, Observable<readonly DishDto[]>>();
  execute(categoryId: string) {
    this.callsByCategory.set(categoryId, (this.callsByCategory.get(categoryId) ?? 0) + 1);
    return (
      this.sourceByCategory.get(categoryId) ??
      of(DISHES_CAT_1.filter((d) => d.categoryId === categoryId))
    );
  }
}

function setup(
  categories?: FakeGetCategories,
  dishes?: FakeGetDishes,
): {
  store: CatalogStore;
  categories: FakeGetCategories;
  dishes: FakeGetDishes;
} {
  const fakeCategories = categories ?? new FakeGetCategories();
  const fakeDishes = dishes ?? new FakeGetDishes();
  TestBed.configureTestingModule({
    providers: [
      CatalogStore,
      { provide: GET_CATEGORIES, useValue: fakeCategories },
      { provide: GET_DISHES_BY_CATEGORY, useValue: fakeDishes },
    ],
  });
  return {
    store: TestBed.inject(CatalogStore),
    categories: fakeCategories,
    dishes: fakeDishes,
  };
}

describe('CatalogStore', () => {
  describe('categories', () => {
    it('loads and exposes the categories', () => {
      const { store } = setup();
      store.loadCategories();
      expect(store.categories()).toEqual(CATEGORIES);
      expect(store.categoriesLoaded()).toBe(true);
      expect(store.categoriesLoading()).toBe(false);
    });

    it('caches: a second load does not re-fetch', () => {
      const { store, categories } = setup();
      store.loadCategories();
      store.loadCategories();
      store.loadCategories();
      expect(categories.calls).toBe(1);
    });

    it('does not re-fetch while a load is in flight', () => {
      const fake = new FakeGetCategories();
      fake.source = new Subject<readonly CategoryDto[]>();
      const { store, categories } = setup(fake);
      store.loadCategories();
      store.loadCategories();
      expect(categories.calls).toBe(1);
      expect(store.categoriesLoading()).toBe(true);
    });

    it('force re-fetches even when already loaded', () => {
      const { store, categories } = setup();
      store.loadCategories();
      store.loadCategories(true);
      expect(categories.calls).toBe(2);
    });

    it('surfaces a typed error and does not mark loaded', () => {
      const fake = new FakeGetCategories();
      fake.source = throwError(() => apiError());
      const { store } = setup(fake);
      store.loadCategories();
      expect(store.categoriesError()?.code).toBe('Http.ServerError');
      expect(store.categoriesLoaded()).toBe(false);
    });

    it('a failed load is retried on the next loadCategories (not cached)', () => {
      const fake = new FakeGetCategories();
      fake.source = throwError(() => apiError());
      const { store, categories } = setup(fake);
      store.loadCategories();
      expect(categories.calls).toBe(1);
      store.loadCategories();
      expect(categories.calls).toBe(2);
    });

    it('flags an empty categories list', () => {
      const fake = new FakeGetCategories();
      fake.source = of([]);
      const { store } = setup(fake);
      store.loadCategories();
      expect(store.categoriesEmpty()).toBe(true);
    });

    it('categoryById finds a loaded category', () => {
      const { store } = setup();
      store.loadCategories();
      expect(store.categoryById('cat-2')()).toEqual({ id: 'cat-2', name: 'Fast food' });
      expect(store.categoryById('missing')()).toBeUndefined();
    });
  });

  describe('dishes by category', () => {
    it('loads and exposes the dishes for a category', () => {
      const { store } = setup();
      store.loadDishes('cat-1');
      expect(store.dishes('cat-1')()).toEqual(DISHES_CAT_1);
    });

    it('caches per category: re-opening a category does not re-fetch', () => {
      const { store, dishes } = setup();
      store.loadDishes('cat-1');
      store.loadDishes('cat-1');
      expect(dishes.callsByCategory.get('cat-1')).toBe(1);
    });

    it('caches each category independently', () => {
      const { store, dishes } = setup();
      store.loadDishes('cat-1');
      store.loadDishes('cat-2');
      store.loadDishes('cat-1');
      expect(dishes.callsByCategory.get('cat-1')).toBe(1);
      expect(dishes.callsByCategory.get('cat-2')).toBe(1);
    });

    it('force re-fetches a category', () => {
      const { store, dishes } = setup();
      store.loadDishes('cat-1');
      store.loadDishes('cat-1', true);
      expect(dishes.callsByCategory.get('cat-1')).toBe(2);
    });

    it('a failed dishes load is retried on the next loadDishes (not cached)', () => {
      const fake = new FakeGetDishes();
      fake.sourceByCategory.set(
        'cat-1',
        throwError(() => apiError()),
      );
      const { store, dishes } = setup(undefined, fake);
      store.loadDishes('cat-1');
      expect(dishes.callsByCategory.get('cat-1')).toBe(1);
      expect(store.dishesError('cat-1')()?.code).toBe('Http.ServerError');
      store.loadDishes('cat-1');
      expect(dishes.callsByCategory.get('cat-1')).toBe(2);
    });

    it('exposes per-category loading and undefined dishes before load', () => {
      const { store } = setup();
      expect(store.dishes('cat-1')()).toBeUndefined();
      expect(store.dishesLoading('cat-1')()).toBe(false);
    });

    it('reading an un-loaded category does not create an entry or write state (NG0600-safe)', () => {
      const { store } = setup();
      const stateBefore = store.state();
      // All four read accessors for a category that was never loaded.
      expect(store.dishes('cat-1')()).toBeUndefined();
      expect(store.dishesLoading('cat-1')()).toBe(false);
      expect(store.dishesError('cat-1')()).toBeUndefined();
      expect(store.dishesEmpty('cat-1')()).toBe(false);
      // No per-category entry was created and the state object reference never changed (no write).
      expect(store.state()).toBe(stateBefore);
      expect(store.state().dishesByCategory.size).toBe(0);
    });

    it('a read taken before load reactively tracks the entry once loadDishes creates it', () => {
      const { store } = setup();
      const dishes = store.dishes('cat-1');
      const loading = store.dishesLoading('cat-1');
      expect(dishes()).toBeUndefined();
      store.loadDishes('cat-1');
      // The same read signals now reflect the freshly-created, loaded entry.
      expect(dishes()).toEqual(DISHES_CAT_1);
      expect(loading()).toBe(false);
    });
  });

  describe('dishNameById (Step 14 menu-name lookup)', () => {
    it('returns undefined for a dish whose taxonomy has not been loaded (fetch-free)', () => {
      const { store, dishes } = setup();
      // Read-only: it must not trigger any dishes fetch.
      expect(store.dishNameById('dish-1')()).toBeUndefined();
      expect(dishes.callsByCategory.size).toBe(0);
    });

    it("resolves a loaded dish's canonical name across cached categories", () => {
      const { store } = setup();
      store.loadDishes('cat-1');
      expect(store.dishNameById('dish-1')()).toBe('Borscht');
      expect(store.dishNameById('missing')()).toBeUndefined();
    });

    it('reactively resolves a name once the dish becomes available', () => {
      const { store } = setup();
      const name = store.dishNameById('dish-1');
      expect(name()).toBeUndefined();
      store.loadDishes('cat-1');
      expect(name()).toBe('Borscht');
    });
  });
});
