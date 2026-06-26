/**
 * TypeScript mirror of the contrast-relevant **primitive** color tokens defined
 * in `_tokens.scss`.
 *
 * This exists so the WCAG-contrast unit check (`tokens.contrast.spec.ts`) and the
 * SCSS theme share one set of hex values: if a brand retune in `_tokens.scss`
 * drops a body-text token pair below AA, the test fails. Keep these values in
 * sync with the `--dp-palette-*` primitives in `_tokens.scss` (only the pairs the
 * test asserts need to be mirrored here).
 */

/** Hex values of the primitive palette entries used by body/secondary text pairs. */
export const TOKEN_HEX = {
  brand40: '#8a4b00',
  neutral10: '#1f1b16',
  neutral30: '#514537',
  neutral50: '#6b6155',
  neutral99: '#fffbf5',
  neutral100: '#ffffff',
  accent40: '#00658f',
  error40: '#ba1a1a',
} as const;

/**
 * Semantic foreground/background pairs that MUST meet WCAG AA for normal-size
 * body text (contrast ratio ≥ 4.5:1). Mirrors the semantic tokens in
 * `_tokens.scss` (e.g. on-surface on surface, on-surface-variant on surface).
 */
export interface ContrastPair {
  readonly name: string;
  readonly foreground: string;
  readonly background: string;
}

export const BODY_TEXT_CONTRAST_PAIRS: readonly ContrastPair[] = [
  {
    name: 'on-surface / surface',
    foreground: TOKEN_HEX.neutral10,
    background: TOKEN_HEX.neutral99,
  },
  {
    name: 'on-surface-variant / surface',
    foreground: TOKEN_HEX.neutral30,
    background: TOKEN_HEX.neutral99,
  },
  {
    name: 'secondary-ink (outline) / surface',
    foreground: TOKEN_HEX.neutral50,
    background: TOKEN_HEX.neutral99,
  },
  { name: 'primary / on-primary', foreground: TOKEN_HEX.neutral100, background: TOKEN_HEX.brand40 },
  { name: 'error / on-error', foreground: TOKEN_HEX.neutral100, background: TOKEN_HEX.error40 },
];

/**
 * Non-text-contrast pairs that must meet the WCAG 2.4.7 focus-indicator threshold
 * (≥ 3:1 against the adjacent surface), kept separate from the 4.5:1 body-text
 * pairs because the requirement (and the asserted ratio) differs. A brand retune
 * that drops the focus ring below 3:1 on the page surface must fail CI.
 *
 * `--dp-color-focus-ring` resolves to `--dp-palette-accent-40` (accent40) and the
 * page background to `--dp-color-surface` → `--dp-palette-neutral-99` (neutral99).
 */
export const FOCUS_INDICATOR_CONTRAST_PAIRS: readonly ContrastPair[] = [
  {
    name: 'focus-ring / surface',
    foreground: TOKEN_HEX.accent40,
    background: TOKEN_HEX.neutral99,
  },
];
