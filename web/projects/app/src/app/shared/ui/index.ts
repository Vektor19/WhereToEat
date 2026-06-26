/**
 * Barrel for the shared presentational UI primitives the features reuse for a consistent
 * loading / error / empty-state experience (DRY). These are dumb (presentational) standalone
 * components driven by inputs from the shared `RequestState` async-state primitive in `core`.
 */
export * from './loading.component';
export * from './error-state.component';
