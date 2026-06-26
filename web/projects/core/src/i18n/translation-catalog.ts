/**
 * The shape of a message catalog (Step 17). A catalog is a plain, arbitrarily-nested object whose
 * leaves are strings (ngx-translate resolves a dotted key like `consumer.results.open` by walking the
 * nesting). Keeping it a plain data type — not a framework object — is what makes the catalogs the
 * view-independent, RN-portable seam the design calls for: the same catalog data feeds the React
 * Native rewrite unchanged.
 */
export interface TranslationCatalog {
  readonly [key: string]: string | TranslationCatalog;
}

/** The catalogs the app bundles, keyed by locale code. Used by the in-memory loader (no HTTP). */
export type CatalogRegistry = Readonly<Record<string, TranslationCatalog>>;
