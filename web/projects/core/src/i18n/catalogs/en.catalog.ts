/**
 * English (`en`) message catalog (Step 17) — a **scaffold placeholder** proving that adding a European
 * locale needs **no architectural change** (design Goal 4 / Risk #7): it is a sibling catalog file and
 * a {@link SUPPORTED_LOCALES} entry, nothing else. It is intentionally **left empty at launch** — only
 * `uk` is populated. Because the locale service sets `uk` as the runtime fallback, any key not present
 * here resolves to the Ukrainian string rather than showing a raw key, so switching to `en` degrades
 * gracefully until the catalog is filled in.
 *
 * To localize for real, populate this object with the same key structure as {@link UK_CATALOG}; no
 * code elsewhere changes.
 */
import type { TranslationCatalog } from '../translation-catalog';

export const EN_CATALOG: TranslationCatalog = {};
