import {
  LOW_REVIEW_NOTE_KEY,
  LOW_REVIEW_THRESHOLD,
  NO_REVIEWS_KEY,
  RatingPresenter,
} from './rating.presenter';

describe('RatingPresenter', () => {
  let presenter: RatingPresenter;

  beforeEach(() => {
    presenter = new RatingPresenter();
  });

  it('presents a confident rating (count at/above threshold) with no note', () => {
    const result = presenter.present(4.3, LOW_REVIEW_THRESHOLD, 'uk');
    expect(result.hasRating).toBe(true);
    expect(result.value).toMatch(/4[.,]3/);
    expect(result.count).toBe(LOW_REVIEW_THRESHOLD);
    expect(result.isLowConfidence).toBe(false);
    expect(result.noteKey).toBeNull();
  });

  it('attaches the discreet low-review note below the threshold (invariant #6)', () => {
    const result = presenter.present(4.8, 2, 'uk');
    expect(result.hasRating).toBe(true);
    expect(result.isLowConfidence).toBe(true);
    expect(result.noteKey).toBe(LOW_REVIEW_NOTE_KEY);
    expect(result.count).toBe(2);
  });

  it('uses a message key for the note, not a hardcoded localized string (Step 17 resolves copy)', () => {
    const result = presenter.present(4.8, 1, 'uk');
    // The note is a stable key, not human copy.
    expect(result.noteKey).toBe(LOW_REVIEW_NOTE_KEY);
    expect(result.noteKey).not.toMatch(/\s/);
  });

  it('models the no-reviews state with the no-reviews key and no value', () => {
    const result = presenter.present(null, 0, 'uk');
    expect(result.hasRating).toBe(false);
    expect(result.value).toBeNull();
    expect(result.count).toBe(0);
    expect(result.noteKey).toBe(NO_REVIEWS_KEY);
  });

  it('treats a present count with a missing smoothed value as no-reviews', () => {
    const result = presenter.present(undefined, 5, 'uk');
    expect(result.hasRating).toBe(false);
    expect(result.noteKey).toBe(NO_REVIEWS_KEY);
  });

  it('formats the value to one decimal, locale-aware', () => {
    expect(presenter.present(4, 50, 'uk').value).toMatch(/4[.,]0/);
    const uk = presenter.present(4.5, 50, 'uk').value;
    const en = presenter.present(4.5, 50, 'en-US').value;
    expect(uk).not.toBe(en); // comma vs dot decimal
  });

  it('normalises a negative / non-integer / non-finite count', () => {
    expect(presenter.present(4, -3, 'uk').count).toBe(0);
    expect(presenter.present(4, 7.9, 'uk').count).toBe(7);
    expect(presenter.present(4, Number.NaN, 'uk').count).toBe(0);
  });

  it('never references a Google rating (our ratings only — invariant #6)', () => {
    const result = presenter.present(4.2, 3, 'uk');
    expect(JSON.stringify(result).toLowerCase()).not.toContain('google');
  });
});
