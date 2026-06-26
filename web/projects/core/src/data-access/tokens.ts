/**
 * Injection tokens for every data-access operation port (one token per endpoint operation).
 *
 * Features inject the **token** (typed to the operation interface), never the concrete
 * `HttpClient` adapter — dependency inversion (the design's RN-portability seam). Each token's
 * `factory` resolves the default `HttpClient` implementation, so the wiring is zero-config:
 * injecting the token yields the adapter, and a test/alternate transport can override the token
 * without touching feature code.
 *
 * Tokens are grouped Public vs Admin to mirror the two logical hosts; the adapter each resolves
 * already composes the correct host prefix through {@link ApiConfig}.
 */
import { InjectionToken, inject } from '@angular/core';

import {
  HttpGetCategoriesOperation,
  HttpGetDishesByCategoryOperation,
  HttpGetRestaurantDetailsOperation,
  type GetCategoriesOperation,
  type GetDishesByCategoryOperation,
  type GetRestaurantDetailsOperation,
} from './public/catalog.operations';
import {
  HttpSearchCategoriesOperation,
  HttpSearchDishesOperation,
  type SearchCategoriesOperation,
  type SearchDishesOperation,
} from './public/search.operations';
import { HttpRecommendOperation, type RecommendOperation } from './public/recommend.operation';
import { HttpGetMapOperation, type GetMapOperation } from './public/map.operation';
import {
  HttpIngestAnalyticsOperation,
  type IngestAnalyticsOperation,
} from './public/analytics-ingest.operation';
import { HttpGetMeOperation, type GetMeOperation } from './public/identity.operation';
import { HttpSubmitRatingOperation, type SubmitRatingOperation } from './public/rating.operation';
import { HttpGetHealthOperation, type GetHealthOperation } from './public/health.operation';
import {
  HttpEditAddressOperation,
  HttpEditMenuItemOperation,
  type EditAddressOperation,
  type EditMenuItemOperation,
} from './admin/catalog-edit.operations';
import {
  HttpSetMenuItemDoNotParseOperation,
  HttpSetRestaurantDoNotUpdateOperation,
  type SetMenuItemDoNotParseOperation,
  type SetRestaurantDoNotUpdateOperation,
} from './admin/protection.operations';
import {
  HttpSetPhotoPermissionOperation,
  type SetPhotoPermissionOperation,
} from './admin/photo.operation';
import {
  HttpCreateAdPlacementOperation,
  HttpGrantVerifiedOperation,
  HttpRevokeVerifiedOperation,
  type CreateAdPlacementOperation,
  type GrantVerifiedOperation,
  type RevokeVerifiedOperation,
} from './admin/monetization.operations';
import {
  HttpGetTrafficOperation,
  HttpGetFunnelOperation,
  HttpGetDemandOperation,
  HttpGetPricePositioningOperation,
  HttpGetRatingsDistributionOperation,
  type GetTrafficOperation,
  type GetFunnelOperation,
  type GetDemandOperation,
  type GetPricePositioningOperation,
  type GetRatingsDistributionOperation,
} from './admin/analytics-dashboard.operations';

// ── Public host ────────────────────────────────────────────────────────────

export const GET_CATEGORIES = new InjectionToken<GetCategoriesOperation>('GET_CATEGORIES', {
  providedIn: 'root',
  factory: () => inject(HttpGetCategoriesOperation),
});

export const GET_DISHES_BY_CATEGORY = new InjectionToken<GetDishesByCategoryOperation>(
  'GET_DISHES_BY_CATEGORY',
  { providedIn: 'root', factory: () => inject(HttpGetDishesByCategoryOperation) },
);

export const GET_RESTAURANT_DETAILS = new InjectionToken<GetRestaurantDetailsOperation>(
  'GET_RESTAURANT_DETAILS',
  { providedIn: 'root', factory: () => inject(HttpGetRestaurantDetailsOperation) },
);

export const SEARCH_CATEGORIES = new InjectionToken<SearchCategoriesOperation>(
  'SEARCH_CATEGORIES',
  {
    providedIn: 'root',
    factory: () => inject(HttpSearchCategoriesOperation),
  },
);

export const SEARCH_DISHES = new InjectionToken<SearchDishesOperation>('SEARCH_DISHES', {
  providedIn: 'root',
  factory: () => inject(HttpSearchDishesOperation),
});

export const RECOMMEND = new InjectionToken<RecommendOperation>('RECOMMEND', {
  providedIn: 'root',
  factory: () => inject(HttpRecommendOperation),
});

export const GET_MAP = new InjectionToken<GetMapOperation>('GET_MAP', {
  providedIn: 'root',
  factory: () => inject(HttpGetMapOperation),
});

export const INGEST_ANALYTICS = new InjectionToken<IngestAnalyticsOperation>('INGEST_ANALYTICS', {
  providedIn: 'root',
  factory: () => inject(HttpIngestAnalyticsOperation),
});

export const GET_ME = new InjectionToken<GetMeOperation>('GET_ME', {
  providedIn: 'root',
  factory: () => inject(HttpGetMeOperation),
});

export const GET_HEALTH = new InjectionToken<GetHealthOperation>('GET_HEALTH', {
  providedIn: 'root',
  factory: () => inject(HttpGetHealthOperation),
});

export const SUBMIT_RATING = new InjectionToken<SubmitRatingOperation>('SUBMIT_RATING', {
  providedIn: 'root',
  factory: () => inject(HttpSubmitRatingOperation),
});

// ── Admin host ─────────────────────────────────────────────────────────────

export const EDIT_MENU_ITEM = new InjectionToken<EditMenuItemOperation>('EDIT_MENU_ITEM', {
  providedIn: 'root',
  factory: () => inject(HttpEditMenuItemOperation),
});

export const EDIT_ADDRESS = new InjectionToken<EditAddressOperation>('EDIT_ADDRESS', {
  providedIn: 'root',
  factory: () => inject(HttpEditAddressOperation),
});

export const SET_MENU_ITEM_DO_NOT_PARSE = new InjectionToken<SetMenuItemDoNotParseOperation>(
  'SET_MENU_ITEM_DO_NOT_PARSE',
  { providedIn: 'root', factory: () => inject(HttpSetMenuItemDoNotParseOperation) },
);

export const SET_RESTAURANT_DO_NOT_UPDATE = new InjectionToken<SetRestaurantDoNotUpdateOperation>(
  'SET_RESTAURANT_DO_NOT_UPDATE',
  { providedIn: 'root', factory: () => inject(HttpSetRestaurantDoNotUpdateOperation) },
);

export const SET_PHOTO_PERMISSION = new InjectionToken<SetPhotoPermissionOperation>(
  'SET_PHOTO_PERMISSION',
  { providedIn: 'root', factory: () => inject(HttpSetPhotoPermissionOperation) },
);

export const GRANT_VERIFIED = new InjectionToken<GrantVerifiedOperation>('GRANT_VERIFIED', {
  providedIn: 'root',
  factory: () => inject(HttpGrantVerifiedOperation),
});

export const REVOKE_VERIFIED = new InjectionToken<RevokeVerifiedOperation>('REVOKE_VERIFIED', {
  providedIn: 'root',
  factory: () => inject(HttpRevokeVerifiedOperation),
});

export const CREATE_AD_PLACEMENT = new InjectionToken<CreateAdPlacementOperation>(
  'CREATE_AD_PLACEMENT',
  { providedIn: 'root', factory: () => inject(HttpCreateAdPlacementOperation) },
);

// ── Admin host — §7.5 analytics dashboard (aggregates-only reads, Step 24) ───

export const GET_TRAFFIC = new InjectionToken<GetTrafficOperation>('GET_TRAFFIC', {
  providedIn: 'root',
  factory: () => inject(HttpGetTrafficOperation),
});

export const GET_FUNNEL = new InjectionToken<GetFunnelOperation>('GET_FUNNEL', {
  providedIn: 'root',
  factory: () => inject(HttpGetFunnelOperation),
});

export const GET_DEMAND = new InjectionToken<GetDemandOperation>('GET_DEMAND', {
  providedIn: 'root',
  factory: () => inject(HttpGetDemandOperation),
});

export const GET_PRICE_POSITIONING = new InjectionToken<GetPricePositioningOperation>(
  'GET_PRICE_POSITIONING',
  { providedIn: 'root', factory: () => inject(HttpGetPricePositioningOperation) },
);

export const GET_RATINGS_DISTRIBUTION = new InjectionToken<GetRatingsDistributionOperation>(
  'GET_RATINGS_DISTRIBUTION',
  { providedIn: 'root', factory: () => inject(HttpGetRatingsDistributionOperation) },
);
