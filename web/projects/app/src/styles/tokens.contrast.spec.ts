import { contrastRatio } from './contrast';
import { BODY_TEXT_CONTRAST_PAIRS, FOCUS_INDICATOR_CONTRAST_PAIRS } from './tokens.values';

const WCAG_AA_BODY_TEXT = 4.5;
const WCAG_FOCUS_INDICATOR = 3;

describe('design-token contrast (WCAG AA)', () => {
  it.each(BODY_TEXT_CONTRAST_PAIRS)(
    'pair "$name" meets AA body-text contrast (>= 4.5:1)',
    ({ foreground, background }) => {
      expect(contrastRatio(foreground, background)).toBeGreaterThanOrEqual(WCAG_AA_BODY_TEXT);
    },
  );

  it.each(FOCUS_INDICATOR_CONTRAST_PAIRS)(
    'focus-indicator pair "$name" meets WCAG 2.4.7 (>= 3:1)',
    ({ foreground, background }) => {
      expect(contrastRatio(foreground, background)).toBeGreaterThanOrEqual(WCAG_FOCUS_INDICATOR);
    },
  );

  it('contrastRatio is symmetric and bounded', () => {
    expect(contrastRatio('#ffffff', '#000000')).toBeCloseTo(21, 0);
    expect(contrastRatio('#000000', '#ffffff')).toBeCloseTo(21, 0);
    expect(contrastRatio('#777777', '#777777')).toBeCloseTo(1, 5);
  });
});
