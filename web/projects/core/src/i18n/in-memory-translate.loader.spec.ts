import { firstValueFrom } from 'rxjs';

import { BUNDLED_CATALOGS, InMemoryTranslateLoader } from './in-memory-translate.loader';

/** Walk a dotted key through a nested catalog the way ngx-translate resolves it, for assertions. */
function resolve(catalog: unknown, dottedKey: string): unknown {
  return dottedKey.split('.').reduce<unknown>((node, segment) => {
    return node !== null && typeof node === 'object'
      ? (node as Record<string, unknown>)[segment]
      : undefined;
  }, catalog);
}

describe('InMemoryTranslateLoader', () => {
  let loader: InMemoryTranslateLoader;

  beforeEach(() => {
    loader = new InMemoryTranslateLoader();
  });

  it('bundles uk (populated) and en (additive-proof placeholder)', () => {
    expect(BUNDLED_CATALOGS['uk']).toBeDefined();
    expect(BUNDLED_CATALOGS['en']).toBeDefined();
  });

  it('serves the uk catalog so a known key resolves to its Ukrainian string', async () => {
    const uk = await firstValueFrom(loader.getTranslation('uk'));
    // Representative deferred keys this step resolved: an error key, a rating note, a geo error.
    expect(resolve(uk, 'errors.notFound')).toBe('Не знайдено.');
    expect(resolve(uk, 'rating.noReviews')).toBe('Поки немає відгуків.');
    expect(resolve(uk, 'consumer.geo.error.timeout')).toBe(
      'Визначення місцезнаходження зайняло забагато часу.',
    );
  });

  it('the uk catalog carries every deferred error / rating / geo key (no raw keys left)', async () => {
    const uk = await firstValueFrom(loader.getTranslation('uk'));
    const required = [
      'errors.validation',
      'errors.unauthorized',
      'errors.forbidden',
      'errors.notFound',
      'errors.server',
      'errors.network',
      'errors.unknown',
      'rating.lowReviewNote',
      'rating.noReviews',
      'consumer.geo.error.permissionDenied',
      'consumer.geo.error.positionUnavailable',
      'consumer.geo.error.timeout',
      'consumer.geo.error.unsupported',
    ];
    for (const key of required) {
      expect(typeof resolve(uk, key)).toBe('string');
    }
  });

  it('falls back to an empty catalog for an unknown locale (ngx-translate then uses the fallback)', async () => {
    const unknown = await firstValueFrom(loader.getTranslation('zz'));
    expect(unknown).toEqual({});
  });
});
