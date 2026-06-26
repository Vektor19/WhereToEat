/**
 * Minimal ambient declaration for the Node `process.env` the e2e specs read for live-stack
 * gating, so the suite typechecks without pulling in the full `@types/node` package (which is
 * not a workspace dependency). Playwright's runtime supplies the real `process` at execution
 * time; this only satisfies the typechecker.
 */
declare const process: {
  readonly env: Record<string, string | undefined>;
};
