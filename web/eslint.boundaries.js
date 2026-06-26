// @ts-check
//
// Import-boundary enforcement for the workspace.
//
// Angular has no runtime "project reference" boundary for lazy routes, so the
// consumer/portal/core module boundaries are enforced here by ESLint. Two
// independent mechanisms:
//
//  1. `boundaries/*` (eslint-plugin-boundaries) classifies every file into an
//     "element type" (core / consumer / portal / app-shell) and declares which
//     element types may import which — forbidding consumer↔portal coupling.
//  2. `no-restricted-imports` forbids reaching into the `core` library through a
//     deep path (`core/...`); `core` may only be imported through its public-api
//     barrel (the bare `'core'` specifier).
//
const boundaries = require('eslint-plugin-boundaries');

// `mode: 'file'` classifies each file individually against the patterns
// (first match wins), so the broad `app-shell` pattern below does not swallow
// the more specific consumer/portal/core trees.
/** Element-type classification, evaluated top-to-bottom (first match wins). */
const elements = [
  // The shared `core` library — consumed only through its public-api barrel.
  { type: 'core', pattern: 'projects/core/src/**/*', mode: 'file' },
  // Shared app-layer presentational primitives (loading / error / empty state, Step 7)
  // both surfaces reuse for a consistent, DRY loading/error experience.
  { type: 'shared', pattern: 'projects/app/src/app/shared/**/*', mode: 'file' },
  // Surface A — consumer search app.
  { type: 'consumer', pattern: 'projects/app/src/app/consumer/**/*', mode: 'file' },
  // Surface B — operator portal (web-only, admin-gated).
  { type: 'portal', pattern: 'projects/app/src/app/portal/**/*', mode: 'file' },
  // The application shell / bootstrap (root component, config, routes).
  { type: 'app-shell', pattern: 'projects/app/src/**/*', mode: 'file' },
];

/**
 * Allowed import edges between element types. Anything not listed is denied by
 * the `boundaries/element-types` rule (`default: 'disallow'`).
 *
 *  - core       → may import only core (a leaf shared layer).
 *  - shared     → may import shared + core (a leaf app-layer UI layer; never a surface).
 *  - consumer   → may import consumer + shared + core. **Never portal.**
 *  - portal     → may import portal + shared + core. **Never consumer.**
 *  - app-shell  → may import everything (it composes the surfaces via lazy routes).
 */
const rules = [
  { from: ['core'], allow: ['core'] },
  { from: ['shared'], allow: ['shared', 'core'] },
  { from: ['consumer'], allow: ['consumer', 'shared', 'core'] },
  { from: ['portal'], allow: ['portal', 'shared', 'core'] },
  { from: ['app-shell'], allow: ['app-shell', 'shared', 'consumer', 'portal', 'core'] },
];

/** Flat-config block applied to all workspace TypeScript files. */
const boundariesConfig = {
  files: ['projects/**/*.ts'],
  plugins: { boundaries },
  settings: {
    'boundaries/elements': elements,
    // Classify dependencies declared via static/dynamic imports.
    'boundaries/dependency-nodes': ['import', 'dynamic-import'],
    // The boundaries rule resolves imported module paths through the import
    // resolver; the TypeScript resolver is required so `.ts` (and the `core`
    // tsconfig path alias) resolve to a classifiable element.
    'import/resolver': {
      typescript: { alwaysTryTypes: true },
      node: true,
    },
  },
  rules: {
    'boundaries/element-types': [
      'error',
      {
        default: 'disallow',
        message: '${file.type} is not allowed to import ${dependency.type}',
        rules,
      },
    ],
    // The `core` library is consumed ONLY through its public-api barrel ('core').
    // Deep imports that reach past the barrel — whether via the path alias
    // (`core/lib/...`) or a relative path into the library tree
    // (`../../core/src/...`) — are forbidden.
    'no-restricted-imports': [
      'error',
      {
        patterns: [
          {
            group: ['core/*', '**/projects/core/*', '**/core/src/**'],
            message:
              "Import from the 'core' public-api barrel ('core'), not from a deep path inside the core library.",
          },
        ],
      },
    ],
  },
};

module.exports = { boundariesConfig };
