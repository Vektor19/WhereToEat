import { isApiErrorEnvelope, type ApiError } from './api-error';
import type {
  CategoryDto,
  ContactLinkDto,
  DishDto,
  MenuItemDto,
  RestaurantDetailsDto,
} from './catalog.models';
import type { IdentityDto } from './identity.models';
import type { MapPayloadDto } from './map.models';

describe('catalog models', () => {
  it('shapes a restaurant with always-present contact links and a menu', () => {
    const category: CategoryDto = { id: 'c-1', name: 'Перші страви' };
    const dish: DishDto = { id: 'd-1', categoryId: category.id, canonicalName: 'Борщ' };
    const contact: ContactLinkDto = { kind: 'site', url: 'https://example.com', label: 'Site' };
    const menuItem: MenuItemDto = {
      id: 'm-1',
      dishId: dish.id,
      priceAmount: 150,
      priceCurrency: 'UAH',
      weight: '350 г',
    };
    const details: RestaurantDetailsDto = {
      id: 'r-1',
      name: 'Borsch House',
      addressLine: 'вул. Хрещатик, 1',
      addressCity: 'Київ',
      contactLinks: [contact],
      menuItems: [menuItem],
    };

    expect(details.contactLinks).toHaveLength(1);
    expect(details.menuItems[0].priceCurrency).toBe('UAH');
  });

  it('allows the optional fields (label, addressCity, weight) to be absent', () => {
    const contact: ContactLinkDto = { kind: 'phone', url: 'tel:+380000000000' };
    const details: RestaurantDetailsDto = {
      id: 'r-2',
      name: 'No City',
      addressLine: 'Somewhere 2',
      contactLinks: [contact],
      menuItems: [],
    };

    expect(details.addressCity).toBeUndefined();
    expect(contact.label).toBeUndefined();
  });
});

describe('MapPayloadDto', () => {
  it('shapes a no-map-data payload with null coordinates', () => {
    const payload: MapPayloadDto = { restaurantId: 'r-1', hasMapData: false };

    expect(payload.hasMapData).toBe(false);
    expect(payload.latitude).toBeUndefined();
  });

  it('shapes a populated payload with coordinates and a deep link', () => {
    const payload: MapPayloadDto = {
      restaurantId: 'r-1',
      hasMapData: true,
      latitude: 50.45,
      longitude: 30.52,
      placeId: 'PLACE',
      mapsDeepLink: 'https://maps.google.com/?q=PLACE',
    };

    expect(payload.hasMapData).toBe(true);
    expect(payload.placeId).toBe('PLACE');
  });
});

describe('IdentityDto', () => {
  it('shapes an admin identity', () => {
    const me: IdentityDto = { subjectId: 'sub-123', roles: ['User', 'Admin'] };

    expect(me.roles).toContain('Admin');
  });
});

describe('api error', () => {
  it('recognizes the backend { error, message } envelope', () => {
    expect(isApiErrorEnvelope({ error: 'Analytics.UnknownKind', message: 'bad' })).toBe(true);
  });

  it('rejects values missing the envelope fields', () => {
    expect(isApiErrorEnvelope({ error: 'only-error' })).toBe(false);
    expect(isApiErrorEnvelope(null)).toBe(false);
    expect(isApiErrorEnvelope('nope')).toBe(false);
  });

  it('shapes a normalized ApiError', () => {
    const err: ApiError = {
      kind: 'not-found',
      status: 404,
      code: 'Catalog.RestaurantNotFound',
      message: 'No such restaurant.',
      messageKey: 'errors.notFound',
    };

    expect(err.kind).toBe('not-found');
    expect(err.status).toBe(404);
    expect(err.messageKey).toBe('errors.notFound');
  });
});
