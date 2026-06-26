import type { RecommendationRequest } from '../domain';
import {
  MAX_LATITUDE,
  MAX_LONGITUDE,
  MIN_LATITUDE,
  MIN_LONGITUDE,
  RecommendValidationService,
} from './recommend-validation.service';
import type { ValidationCode } from './recommend-validation.service';

describe('RecommendValidationService', () => {
  let service: RecommendValidationService;

  beforeEach(() => {
    service = new RecommendValidationService();
  });

  /** Helper to build a minimal valid request, overriding fields per test. */
  function request(overrides: Partial<RecommendationRequest> = {}): RecommendationRequest {
    return {
      items: [{ categoryId: 'cat-1' }],
      match: 'or',
      sort: 'price',
      filters: [],
      ...overrides,
    };
  }

  function codes(result: { errors: readonly { code: ValidationCode }[] }): ValidationCode[] {
    return result.errors.map((e) => e.code);
  }

  describe('validateRequest — selection', () => {
    it('accepts a single category selection', () => {
      const result = service.validateRequest(request());
      expect(result.valid).toBe(true);
      expect(result.errors).toEqual([]);
    });

    it('accepts a single dish selection', () => {
      const result = service.validateRequest(request({ items: [{ dishId: 'dish-1' }] }));
      expect(result.valid).toBe(true);
    });

    it('accepts a mixed category + dish selection', () => {
      const result = service.validateRequest(
        request({ items: [{ categoryId: 'cat-1' }, { dishId: 'dish-1' }] }),
      );
      expect(result.valid).toBe(true);
    });

    it('rejects an empty selection', () => {
      const result = service.validateRequest(request({ items: [] }));
      expect(result.valid).toBe(false);
      expect(codes(result)).toContain('Recommend.EmptySelection');
    });

    it('rejects an item with neither id (runtime-malformed input)', () => {
      const result = service.validateRequest(
        request({ items: [{} as unknown as { categoryId: string }] }),
      );
      expect(result.valid).toBe(false);
      expect(codes(result)).toContain('Recommend.InvalidItem');
    });

    it('rejects an item with both ids (runtime-malformed input)', () => {
      const both = { categoryId: 'cat-1', dishId: 'dish-1' } as unknown as { categoryId: string };
      const result = service.validateRequest(request({ items: [both] }));
      expect(result.valid).toBe(false);
      expect(codes(result)).toContain('Recommend.InvalidItem');
    });

    it('rejects an item with an empty-string id', () => {
      const result = service.validateRequest(request({ items: [{ categoryId: '   ' }] }));
      expect(result.valid).toBe(false);
      expect(codes(result)).toContain('Recommend.InvalidItem');
    });
  });

  describe('validateRequest — match / sort / filter keys', () => {
    it('rejects an unknown match mode', () => {
      const result = service.validateRequest(request({ match: 'xor' as unknown as 'or' }));
      expect(codes(result)).toContain('Recommend.UnknownMatch');
    });

    it('accepts both known match modes', () => {
      expect(service.validateRequest(request({ match: 'or' })).valid).toBe(true);
      expect(service.validateRequest(request({ match: 'and' })).valid).toBe(true);
    });

    it('rejects an unknown sort mode', () => {
      const result = service.validateRequest(request({ sort: 'cheapest' as unknown as 'price' }));
      expect(codes(result)).toContain('Recommend.UnknownSort');
    });

    it('accepts every documented sort mode', () => {
      for (const sort of ['price', 'distance', 'rating', 'price-quality', 'best'] as const) {
        expect(service.validateRequest(request({ sort })).valid).toBe(true);
      }
    });

    it('rejects an unknown filter key', () => {
      const result = service.validateRequest(
        request({ filters: [{ key: 'vegan' as unknown as 'price', value: 'true' }] }),
      );
      expect(codes(result)).toContain('Recommend.UnknownFilterKey');
    });

    it('accepts composed price + rating filters', () => {
      const result = service.validateRequest(
        request({
          filters: [
            { key: 'price', value: '200' },
            { key: 'rating', value: '4' },
          ],
        }),
      );
      expect(result.valid).toBe(true);
    });
  });

  describe('validateRequest — userGeo range', () => {
    it('accepts an omitted userGeo (opt-in geo)', () => {
      expect(service.validateRequest(request({ userGeo: undefined })).valid).toBe(true);
    });

    it('accepts in-range coordinates', () => {
      const result = service.validateRequest(
        request({ userGeo: { latitude: 50.45, longitude: 30.52 } }),
      );
      expect(result.valid).toBe(true);
    });

    it('accepts the inclusive boundary coordinates', () => {
      const result = service.validateRequest(
        request({ userGeo: { latitude: MIN_LATITUDE, longitude: MAX_LONGITUDE } }),
      );
      expect(result.valid).toBe(true);
    });

    it('rejects a latitude above the max', () => {
      const result = service.validateRequest(
        request({ userGeo: { latitude: MAX_LATITUDE + 0.1, longitude: 0 } }),
      );
      expect(codes(result)).toContain('Recommend.InvalidUserGeo');
    });

    it('rejects a longitude below the min', () => {
      const result = service.validateRequest(
        request({ userGeo: { latitude: 0, longitude: MIN_LONGITUDE - 0.1 } }),
      );
      expect(codes(result)).toContain('Recommend.InvalidUserGeo');
    });

    it('rejects a non-finite coordinate', () => {
      const result = service.validateRequest(
        request({ userGeo: { latitude: Number.NaN, longitude: 0 } }),
      );
      expect(codes(result)).toContain('Recommend.InvalidUserGeo');
    });
  });

  it('aggregates multiple problems in one result', () => {
    const result = service.validateRequest(
      request({
        items: [],
        match: 'bad' as unknown as 'or',
        userGeo: { latitude: 999, longitude: 0 },
      }),
    );
    expect(result.valid).toBe(false);
    expect(codes(result)).toEqual(
      expect.arrayContaining([
        'Recommend.EmptySelection',
        'Recommend.UnknownMatch',
        'Recommend.InvalidUserGeo',
      ]),
    );
  });

  describe('focused validators', () => {
    it('validateSelection rejects empty and accepts non-empty', () => {
      expect(service.validateSelection([]).valid).toBe(false);
      expect(service.validateSelection([{ dishId: 'd' }]).valid).toBe(true);
    });

    it('validateUserGeo enforces the range', () => {
      expect(service.validateUserGeo({ latitude: 10, longitude: 20 }).valid).toBe(true);
      expect(service.validateUserGeo({ latitude: 91, longitude: 20 }).valid).toBe(false);
    });

    it('isKnown* guards reflect the contract unions', () => {
      expect(service.isKnownMatch('and')).toBe(true);
      expect(service.isKnownMatch('nope')).toBe(false);
      expect(service.isKnownSort('best')).toBe(true);
      expect(service.isKnownSort('nope')).toBe(false);
      expect(service.isKnownFilterKey('rating')).toBe(true);
      expect(service.isKnownFilterKey('nope')).toBe(false);
    });
  });
});
