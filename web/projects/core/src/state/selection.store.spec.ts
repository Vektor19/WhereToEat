import { TestBed } from '@angular/core/testing';

import type { SelectedItem, SortMode } from '../domain';
import { RecommendRequestBuilder } from '../recommend/recommend-request.builder';
import { RecommendValidationService } from '../validation/recommend-validation.service';
import { SelectionStore } from './selection.store';

function createStore(): SelectionStore {
  TestBed.configureTestingModule({
    providers: [SelectionStore, RecommendRequestBuilder, RecommendValidationService],
  });
  return TestBed.inject(SelectionStore);
}

describe('SelectionStore', () => {
  let store: SelectionStore;

  beforeEach(() => {
    store = createStore();
  });

  describe('defaults', () => {
    it('starts empty with the default match=or and sort=best', () => {
      expect(store.items()).toEqual([]);
      expect(store.match()).toBe('or');
      expect(store.sort()).toBe('best');
      expect(store.filters()).toEqual([]);
      expect(store.userGeo()).toBeUndefined();
      expect(store.hasSelection()).toBe(false);
      expect(store.count()).toBe(0);
    });
  });

  describe('addItem / removeItem / dedupe', () => {
    it('adds a category and a dish', () => {
      store.addItem({ categoryId: 'cat-1' });
      store.addItem({ dishId: 'dish-1' });
      expect(store.items()).toEqual([{ categoryId: 'cat-1' }, { dishId: 'dish-1' }]);
      expect(store.count()).toBe(2);
      expect(store.hasSelection()).toBe(true);
    });

    it('de-duplicates the same category', () => {
      store.addItem({ categoryId: 'cat-1' });
      store.addItem({ categoryId: 'cat-1' });
      expect(store.items()).toEqual([{ categoryId: 'cat-1' }]);
    });

    it('de-duplicates the same dish', () => {
      store.addItem({ dishId: 'dish-1' });
      store.addItem({ dishId: 'dish-1' });
      expect(store.items()).toEqual([{ dishId: 'dish-1' }]);
    });

    it('treats a category id and a dish id of the same string as distinct items', () => {
      store.addItem({ categoryId: 'x' });
      store.addItem({ dishId: 'x' });
      expect(store.count()).toBe(2);
    });

    it('rejects an invalid item (neither id)', () => {
      store.addItem({} as unknown as SelectedItem);
      expect(store.items()).toEqual([]);
    });

    it('rejects an ambiguous item (both ids)', () => {
      store.addItem({ categoryId: 'c', dishId: 'd' } as unknown as SelectedItem);
      expect(store.items()).toEqual([]);
    });

    it('removes a selected item', () => {
      store.addItem({ categoryId: 'cat-1' });
      store.addItem({ dishId: 'dish-1' });
      store.removeItem({ categoryId: 'cat-1' });
      expect(store.items()).toEqual([{ dishId: 'dish-1' }]);
    });

    it('removeItem is a no-op when the item is not present', () => {
      store.addItem({ categoryId: 'cat-1' });
      store.removeItem({ dishId: 'dish-9' });
      expect(store.items()).toEqual([{ categoryId: 'cat-1' }]);
    });

    it('isSelected reflects membership', () => {
      store.addItem({ categoryId: 'cat-1' });
      expect(store.isSelected({ categoryId: 'cat-1' })).toBe(true);
      expect(store.isSelected({ dishId: 'cat-1' })).toBe(false);
    });
  });

  describe('clear', () => {
    it('clearItems clears items but keeps match/sort/filters/geo', () => {
      store.addItem({ categoryId: 'cat-1' });
      store.setMatch('and');
      store.setSort('price');
      store.setFilter('price', '200');
      store.setGeo({ latitude: 50, longitude: 30 });

      store.clearItems();

      expect(store.items()).toEqual([]);
      expect(store.match()).toBe('and');
      expect(store.sort()).toBe('price');
      expect(store.filters()).toEqual([{ key: 'price', value: '200' }]);
      expect(store.userGeo()).toEqual({ latitude: 50, longitude: 30 });
    });

    it('clear resets everything to defaults', () => {
      store.addItem({ categoryId: 'cat-1' });
      store.setMatch('and');
      store.setSort('rating');
      store.setFilter('rating', '4');
      store.setGeo({ latitude: 50, longitude: 30 });

      store.clear();

      expect(store.items()).toEqual([]);
      expect(store.match()).toBe('or');
      expect(store.sort()).toBe('best');
      expect(store.filters()).toEqual([]);
      expect(store.userGeo()).toBeUndefined();
    });
  });

  describe('match / sort', () => {
    it('sets a known match mode', () => {
      store.setMatch('and');
      expect(store.match()).toBe('and');
    });

    it('rejects an unknown match mode', () => {
      store.setMatch('xor' as unknown as 'or');
      expect(store.match()).toBe('or');
    });

    it('sets each known sort mode', () => {
      const sorts: SortMode[] = ['price', 'distance', 'rating', 'price-quality', 'best'];
      for (const sort of sorts) {
        store.setSort(sort);
        expect(store.sort()).toBe(sort);
      }
    });

    it('rejects an unknown sort mode', () => {
      store.setSort('cheapest' as unknown as SortMode);
      expect(store.sort()).toBe('best');
    });
  });

  describe('filters', () => {
    it('applies a filter by key', () => {
      store.setFilter('price', '200');
      expect(store.filters()).toEqual([{ key: 'price', value: '200' }]);
    });

    it('replaces (does not duplicate) a filter when re-applied by the same key', () => {
      store.setFilter('price', '200');
      store.setFilter('price', '150');
      expect(store.filters()).toEqual([{ key: 'price', value: '150' }]);
    });

    it('keeps distinct filter keys', () => {
      store.setFilter('price', '200');
      store.setFilter('rating', '4');
      expect(store.filters()).toEqual([
        { key: 'price', value: '200' },
        { key: 'rating', value: '4' },
      ]);
    });

    it('normalises an omitted value to null', () => {
      store.setFilter('price');
      expect(store.filters()).toEqual([{ key: 'price', value: null }]);
    });

    it('rejects an unknown filter key', () => {
      store.setFilter('vegan' as unknown as 'price', 'true');
      expect(store.filters()).toEqual([]);
    });

    it('removes a filter by key', () => {
      store.setFilter('price', '200');
      store.setFilter('rating', '4');
      store.removeFilter('price');
      expect(store.filters()).toEqual([{ key: 'rating', value: '4' }]);
    });

    it('clears all filters', () => {
      store.setFilter('price', '200');
      store.setFilter('rating', '4');
      store.clearFilters();
      expect(store.filters()).toEqual([]);
    });
  });

  describe('geo', () => {
    it('sets a valid userGeo', () => {
      store.setGeo({ latitude: 50.45, longitude: 30.52 });
      expect(store.userGeo()).toEqual({ latitude: 50.45, longitude: 30.52 });
    });

    it('rejects an out-of-range userGeo', () => {
      store.setGeo({ latitude: 200, longitude: 0 });
      expect(store.userGeo()).toBeUndefined();
    });

    it('clearGeo drops userGeo entirely', () => {
      store.setGeo({ latitude: 50, longitude: 30 });
      store.clearGeo();
      expect(store.userGeo()).toBeUndefined();
    });

    it('a set→clear→build round-trip omits userGeo from the wire body (invariant #11)', () => {
      store.setGeo({ latitude: 50, longitude: 30 });
      store.clearGeo();
      store.addItem({ categoryId: 'cat-1' });

      const result = store.buildRequest();
      expect(result.valid).toBe(true);
      if (result.valid) {
        expect('userGeo' in result.request).toBe(false);
      }
    });
  });

  describe('buildRequest — feeds the request builder', () => {
    it('returns typed errors for an empty selection', () => {
      const result = store.buildRequest();
      expect(result.valid).toBe(false);
      if (!result.valid) {
        expect(result.errors.map((e) => e.code)).toContain('Recommend.EmptySelection');
      }
    });

    it('builds the exact request body from the current selection', () => {
      store.addItem({ categoryId: 'cat-1' });
      store.addItem({ dishId: 'dish-1' });
      store.setMatch('and');
      store.setSort('price');
      store.setFilter('price', '200');
      store.setGeo({ latitude: 50.45, longitude: 30.52 });

      const result = store.buildRequest();
      expect(result.valid).toBe(true);
      if (result.valid) {
        expect(result.request).toEqual({
          items: [{ categoryId: 'cat-1' }, { dishId: 'dish-1' }],
          match: 'and',
          sort: 'price',
          filters: [{ key: 'price', value: '200' }],
          userGeo: { latitude: 50.45, longitude: 30.52 },
        });
      }
    });

    it('omits userGeo from the request when geo is off', () => {
      store.addItem({ categoryId: 'cat-1' });
      const result = store.buildRequest();
      expect(result.valid).toBe(true);
      if (result.valid) {
        expect('userGeo' in result.request).toBe(false);
      }
    });
  });
});
