/**
 * Map payload model — mirrors the backend `MapPayloadDto` for the "Глянути на карті" affordance
 * (§5.7). Assembled **only** from fields we store: our OSM-sourced coordinates, the optional
 * Google Place ID, and the optional Maps deep-link. It is live-only and **nothing is cached**;
 * no Google rating and no Google-sourced coordinate ever appear here (invariants #6/#7).
 *
 * `hasMapData` is `false` when the restaurant has no stored coordinates — the endpoint returns
 * this "no map payload" shape rather than geocoding, and the coordinate fields are then null.
 */
export interface MapPayloadDto {
  readonly restaurantId: string;
  readonly hasMapData: boolean;
  readonly latitude?: number | null;
  readonly longitude?: number | null;
  readonly placeId?: string | null;
  readonly mapsDeepLink?: string | null;
}
