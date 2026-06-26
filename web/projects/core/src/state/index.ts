/**
 * Barrel for the `core` state layer — the hand-rolled signal-store base and the three cohesive,
 * view-independent stores (catalog / selection / results) that hold the deterministic selection
 * flow and the recommend result (Step 9). Re-exported through the library's public-api so the `app`
 * consumes them through the one boundary (deep imports past the barrel are forbidden by the
 * import-boundary lint rules).
 */
export * from './signal-store.base';
export * from './catalog.store';
export * from './selection.store';
export * from './results.store';
