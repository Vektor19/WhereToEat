/*
 * Public API surface of the `core` library.
 *
 * Everything the `app` consumes from `core` MUST be re-exported here. Reaching
 * into `core` internals (deep imports past this barrel) is forbidden by the
 * ESLint import-boundary rules. Later steps add domain models, data-access
 * interfaces, business-logic services and signal stores to this surface.
 */
export * from './lib/core-info';
export * from './config/app-config.token';
export * from './config/api-config';
export * from './domain';
export * from './data-access';
export * from './auth';
export * from './http';
export * from './ui-state';
export * from './analytics';
export * from './validation';
export * from './recommend';
export * from './format';
export * from './geo';
export * from './i18n';
export * from './state';
export * from './map';
