/**
 * Data-driven operator-portal navigation (Step 18).
 *
 * The portal side-nav is a list, not hardcoded markup, so the Step 19/20/24 management features add a
 * nav entry here rather than editing {@link PortalShellComponent}. Each entry is a relative router
 * link into the portal subtree, a Material icon, and an i18n label key (resolved from the runtime
 * catalogs). `exact` controls `routerLinkActive` matching — `true` for the dashboard root so it is not
 * marked active for every child route.
 *
 * The feature routes these paths point at are registered by later steps (menu / protection / address /
 * photos / verified / ads / analytics). Until then the links resolve to the portal home placeholder;
 * the nav is the scaffold the features plug into. A future owner persona (design Risk #3) can render a
 * filtered subset of this same list without restructuring.
 */
export interface PortalNavItem {
  /** Router link relative to the portal subtree root (`/portal`). Empty string = the dashboard home. */
  readonly path: string;
  /** Material icon ligature shown beside the label. */
  readonly icon: string;
  /** i18n catalog key for the visible label. */
  readonly labelKey: string;
  /** Stable suffix for the `data-testid` (decoupled from copy/path) used in tests. */
  readonly testId: string;
  /** `routerLinkActive` exact matching — `true` only for the dashboard root. */
  readonly exact: boolean;
}

export const PORTAL_NAV: readonly PortalNavItem[] = [
  {
    path: '',
    icon: 'dashboard',
    labelKey: 'portal.nav.dashboard',
    testId: 'dashboard',
    exact: true,
  },
  {
    path: 'menu',
    icon: 'restaurant_menu',
    labelKey: 'portal.nav.menu',
    testId: 'menu',
    exact: false,
  },
  {
    path: 'protection',
    icon: 'shield',
    labelKey: 'portal.nav.protection',
    testId: 'protection',
    exact: false,
  },
  {
    path: 'address',
    icon: 'place',
    labelKey: 'portal.nav.address',
    testId: 'address',
    exact: false,
  },
  {
    path: 'photos',
    icon: 'photo_library',
    labelKey: 'portal.nav.photos',
    testId: 'photos',
    exact: false,
  },
  {
    path: 'verified',
    icon: 'verified',
    labelKey: 'portal.nav.verified',
    testId: 'verified',
    exact: false,
  },
  { path: 'ads', icon: 'campaign', labelKey: 'portal.nav.ads', testId: 'ads', exact: false },
  {
    path: 'analytics',
    icon: 'insights',
    labelKey: 'portal.nav.analytics',
    testId: 'analytics',
    exact: false,
  },
];
