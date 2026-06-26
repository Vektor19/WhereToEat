import {
  isCategorySelection,
  isDishSelection,
  type CategorySelection,
  type DishSelection,
  type RecommendationRequest,
  type RecommendedRestaurantDto,
  type SelectedItem,
} from './recommendation.models';

/**
 * `SelectedItem` must enforce "exactly one of categoryId/dishId" at the type level (mirroring the
 * backend's private `SelectedItem` constructor). The `@ts-expect-error` assertions are the
 * compile-time half — they FAIL THE BUILD if the union ever allows both-ids or neither-ids — and
 * the runtime guard checks are the runtime half.
 */
describe('SelectedItem', () => {
  it('narrows a category selection via the type guard', () => {
    const item: SelectedItem = { categoryId: 'cat-1' };

    expect(isCategorySelection(item)).toBe(true);
    expect(isDishSelection(item)).toBe(false);
  });

  it('narrows a dish selection via the type guard', () => {
    const item: SelectedItem = { dishId: 'dish-1' };

    expect(isDishSelection(item)).toBe(true);
    expect(isCategorySelection(item)).toBe(false);
  });

  it('forbids a both-ids selection at compile time', () => {
    // @ts-expect-error — both ids present is not assignable to SelectedItem (ambiguous selection).
    const both: SelectedItem = { categoryId: 'cat-1', dishId: 'dish-1' };

    // Reference it so the assignment is not dead code; the value is intentionally invalid-typed.
    expect(both).toBeDefined();
  });

  it('forbids a neither-id selection at compile time', () => {
    // @ts-expect-error — no id present is not assignable to SelectedItem (empty selection).
    const neither: SelectedItem = {};

    expect(neither).toBeDefined();
  });

  it('a category selection cannot also carry a dishId at compile time', () => {
    // @ts-expect-error — CategorySelection forbids dishId (`dishId?: never`).
    const bad: CategorySelection = { categoryId: 'cat-1', dishId: 'dish-1' };

    expect(bad).toBeDefined();
  });

  it('a dish selection cannot also carry a categoryId at compile time', () => {
    // @ts-expect-error — DishSelection forbids categoryId (`categoryId?: never`).
    const bad: DishSelection = { dishId: 'dish-1', categoryId: 'cat-1' };

    expect(bad).toBeDefined();
  });
});

describe('RecommendationRequest', () => {
  it('builds a well-typed request with mixed selections and a geo', () => {
    const request: RecommendationRequest = {
      items: [{ categoryId: 'cat-1' }, { dishId: 'dish-1' }],
      match: 'or',
      sort: 'price',
      filters: [{ key: 'price', value: '150' }],
      userGeo: { latitude: 50.45, longitude: 30.52 },
    };

    expect(request.items.length).toBe(2);
    expect(request.userGeo?.latitude).toBe(50.45);
  });

  it('omits userGeo entirely when geolocation is not provided', () => {
    const request: RecommendationRequest = {
      items: [{ dishId: 'dish-1' }],
      match: 'and',
      sort: 'best',
      filters: [],
    };

    expect(request.userGeo).toBeUndefined();
  });
});

describe('RecommendedRestaurantDto', () => {
  it('carries the engine figures, with optional fields absent when not computed', () => {
    const restaurant: RecommendedRestaurantDto = {
      restaurantId: 'r-1',
      name: 'Borsch House',
      ratingCount: 0,
      coverage: 1,
    };

    expect(restaurant.basketPriceAmount).toBeUndefined();
    expect(restaurant.distanceKm).toBeUndefined();
    expect(restaurant.coverage).toBe(1);
  });
});
