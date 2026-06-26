import { TestBed } from '@angular/core/testing';

import type { MatchMode, SortMode } from '../domain';
import { isCategorySelection, isDishSelection } from '../domain';
import { RecommendValidationService } from '../validation/recommend-validation.service';
import { RecommendRequestBuilder } from './recommend-request.builder';
import type { RecommendRequestInput } from './recommend-request.builder';

function createBuilder(): RecommendRequestBuilder {
  TestBed.configureTestingModule({
    providers: [RecommendRequestBuilder, RecommendValidationService],
  });
  return TestBed.inject(RecommendRequestBuilder);
}

describe('RecommendRequestBuilder', () => {
  let builder: RecommendRequestBuilder;

  beforeEach(() => {
    builder = createBuilder();
  });

  describe('build — request shape', () => {
    it('builds a category-only OR/price request', () => {
      const request = builder.build({
        items: [{ categoryId: 'cat-1' }],
        match: 'or',
        sort: 'price',
      });

      expect(request.items).toEqual([{ categoryId: 'cat-1' }]);
      expect(request.match).toBe('or');
      expect(request.sort).toBe('price');
      expect(request.filters).toEqual([]);
      expect(request.userGeo).toBeUndefined();
    });

    it('builds a dish-only AND request', () => {
      const request = builder.build({
        items: [{ dishId: 'dish-1' }, { dishId: 'dish-2' }],
        match: 'and',
        sort: 'best',
      });

      expect(request.items.every(isDishSelection)).toBe(true);
      expect(request.match).toBe('and');
      expect(request.sort).toBe('best');
    });

    it('builds a mixed category + dish request preserving exactly-one-of id per item', () => {
      const request = builder.build({
        items: [{ categoryId: 'cat-1' }, { dishId: 'dish-1' }],
        match: 'or',
        sort: 'distance',
      });

      expect(isCategorySelection(request.items[0])).toBe(true);
      expect(isDishSelection(request.items[1])).toBe(true);
      // No item ever carries both ids.
      for (const item of request.items) {
        const hasCategory = isCategorySelection(item);
        const hasDish = isDishSelection(item);
        expect(hasCategory).not.toBe(hasDish);
      }
    });

    it('includes userGeo only when supplied (omitted entirely when geo is off)', () => {
      const withoutGeo = builder.build({
        items: [{ categoryId: 'cat-1' }],
        match: 'or',
        sort: 'price',
      });
      expect('userGeo' in withoutGeo).toBe(false);

      const withGeo = builder.build({
        items: [{ categoryId: 'cat-1' }],
        match: 'or',
        sort: 'price',
        userGeo: { latitude: 50.45, longitude: 30.52 },
      });
      expect(withGeo.userGeo).toEqual({ latitude: 50.45, longitude: 30.52 });
    });

    it('passes composable filters through', () => {
      const request = builder.build({
        items: [{ categoryId: 'cat-1' }],
        match: 'or',
        sort: 'price-quality',
        filters: [
          { key: 'price', value: '200' },
          { key: 'rating', value: '4' },
        ],
      });
      expect(request.filters).toEqual([
        { key: 'price', value: '200' },
        { key: 'rating', value: '4' },
      ]);
    });

    it('does not alias the caller arrays (defensive copies)', () => {
      const items = [{ categoryId: 'cat-1' }];
      const filters = [{ key: 'price' as const, value: '10' }];
      const request = builder.build({ items, match: 'or', sort: 'price', filters });

      expect(request.items).not.toBe(items);
      expect(request.filters).not.toBe(filters);
    });
  });

  describe('build — table across modes / sorts', () => {
    const matches: MatchMode[] = ['or', 'and'];
    const sorts: SortMode[] = ['price', 'distance', 'rating', 'price-quality', 'best'];

    for (const match of matches) {
      for (const sort of sorts) {
        it(`produces a well-formed request for match=${match} sort=${sort}`, () => {
          const input: RecommendRequestInput = {
            items: [{ categoryId: 'cat-1' }, { dishId: 'dish-1' }],
            match,
            sort,
          };
          const request = builder.build(input);
          expect(request.match).toBe(match);
          expect(request.sort).toBe(sort);
          expect(request.items.length).toBe(2);
        });
      }
    }
  });

  describe('tryBuild — fail-fast validation', () => {
    it('returns the built request when valid', () => {
      const result = builder.tryBuild({
        items: [{ categoryId: 'cat-1' }],
        match: 'or',
        sort: 'price',
      });
      expect(result.valid).toBe(true);
      if (result.valid) {
        expect(result.request.items).toEqual([{ categoryId: 'cat-1' }]);
      }
    });

    it('returns typed errors for an empty selection', () => {
      const result = builder.tryBuild({ items: [], match: 'or', sort: 'price' });
      expect(result.valid).toBe(false);
      if (!result.valid) {
        expect(result.errors.map((e) => e.code)).toContain('Recommend.EmptySelection');
      }
    });

    it('returns typed errors for an out-of-range userGeo', () => {
      const result = builder.tryBuild({
        items: [{ categoryId: 'cat-1' }],
        match: 'or',
        sort: 'price',
        userGeo: { latitude: 200, longitude: 0 },
      });
      expect(result.valid).toBe(false);
      if (!result.valid) {
        expect(result.errors.map((e) => e.code)).toContain('Recommend.InvalidUserGeo');
      }
    });

    it('returns typed errors for an unknown sort key', () => {
      const result = builder.tryBuild({
        items: [{ categoryId: 'cat-1' }],
        match: 'or',
        sort: 'cheapest' as unknown as SortMode,
      });
      expect(result.valid).toBe(false);
      if (!result.valid) {
        expect(result.errors.map((e) => e.code)).toContain('Recommend.UnknownSort');
      }
    });
  });
});
