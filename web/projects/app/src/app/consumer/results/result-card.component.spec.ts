import { TestBed, type ComponentFixture } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { LOW_REVIEW_NOTE_KEY, NO_REVIEWS_KEY, type RatingPresentation } from 'core';

import { ResultCardComponent } from './result-card.component';
import type { ResultCardViewModel } from './result-card.view-model';

/** A confident-rating presentation (count above the low-review threshold → no note). */
const CONFIDENT_RATING: RatingPresentation = {
  hasRating: true,
  value: '4,5',
  count: 42,
  isLowConfidence: false,
  noteKey: null,
};

/** A low-confidence rating presentation (some reviews below threshold → low-review note). */
const LOW_RATING: RatingPresentation = {
  hasRating: true,
  value: '5,0',
  count: 2,
  isLowConfidence: true,
  noteKey: LOW_REVIEW_NOTE_KEY,
};

/** A no-reviews presentation (no rating yet → the `—` dash + no-reviews note). */
const NO_RATING: RatingPresentation = {
  hasRating: false,
  value: null,
  count: 0,
  isLowConfidence: true,
  noteKey: NO_REVIEWS_KEY,
};

function baseVm(overrides: Partial<ResultCardViewModel> = {}): ResultCardViewModel {
  return {
    restaurantId: 'r-1',
    name: 'Smачно',
    position: 1,
    basketPrice: '150,00 ₴',
    rating: CONFIDENT_RATING,
    distance: '1,2 km',
    coverageCovered: 3,
    coverageTotal: 3,
    photo: { isReal: false, src: 'generic-dish.svg', alt: 'Smачно' },
    isAd: false,
    ...overrides,
  };
}

describe('ResultCardComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ResultCardComponent],
      providers: [provideNoopAnimations()],
    }).compileComponents();
  });

  function render(vm: ResultCardViewModel): ComponentFixture<ResultCardComponent> {
    const fixture = TestBed.createComponent(ResultCardComponent);
    fixture.componentRef.setInput('vm', vm);
    fixture.detectChanges();
    return fixture;
  }

  function text(fixture: ComponentFixture<ResultCardComponent>, testid: string): string | null {
    const el = fixture.nativeElement.querySelector(`[data-testid="${testid}"]`);
    return el === null ? null : (el.textContent ?? '').trim();
  }

  it('renders the basket price with NO ≈ marker and NO per-card price disclaimer', () => {
    const fixture = render(baseVm({ basketPrice: '150,00 ₴' }));
    const price = text(fixture, 'result-price');
    expect(price).toBe('150,00 ₴');
    // Invariant #12 / §10: no per-card approximation marker or disclaimer copy.
    const cardText = fixture.nativeElement.textContent as string;
    expect(cardText).not.toContain('≈');
    expect(cardText.toLowerCase()).not.toContain('орієнтов');
    expect(cardText.toLowerCase()).not.toContain('неточ');
  });

  it('renders nothing for an absent basket price (never "0")', () => {
    const fixture = render(baseVm({ basketPrice: null }));
    expect(text(fixture, 'result-price')).toBeNull();
  });

  it('shows our smoothed rating value + count without the low-review note above threshold', () => {
    const fixture = render(baseVm({ rating: CONFIDENT_RATING }));
    expect(text(fixture, 'result-rating')).toContain('4,5');
    expect(text(fixture, 'result-rating')).toContain('(42)');
    expect(text(fixture, 'result-rating-note')).toBeNull();
  });

  it('shows the discreet low-review note below threshold', () => {
    const fixture = render(baseVm({ rating: LOW_RATING }));
    const note = text(fixture, 'result-rating-note');
    expect(note).not.toBeNull();
    expect(note).toContain('2');
  });

  it('renders the "—" dash and the no-reviews note when the venue has no rating', () => {
    const fixture = render(baseVm({ rating: NO_RATING }));
    // No smoothed value/count — the rating slot shows the em-dash placeholder, never "0".
    const rating = text(fixture, 'result-rating');
    expect(rating).toContain('—');
    expect(rating).not.toContain('(0)');
    // The discreet no-reviews note (the second RATING_NOTE_FALLBACK branch) is present.
    const note = text(fixture, 'result-rating-note');
    expect(note).not.toBeNull();
    expect((note ?? '').length).toBeGreaterThan(0);
  });

  it('exposes a consolidated screen-reader label for the rating (decorative icon aria-hidden)', () => {
    const fixture = render(baseVm({ rating: CONFIDENT_RATING }));
    const ratingEl = fixture.nativeElement.querySelector('[data-testid="result-rating"]');
    // The star icon is decorative.
    expect(ratingEl.querySelector('mat-icon').getAttribute('aria-hidden')).toBe('true');
    // The visual value/count fragments are hidden from AT (the sr-only phrase carries them).
    const value = ratingEl.querySelector('.dp-card__rating-value');
    expect(value.getAttribute('aria-hidden')).toBe('true');
    // A single sr-only phrase consolidates value + count for assistive tech.
    const srLabel = ratingEl.querySelector('.dp-sr-only');
    expect(srLabel).not.toBeNull();
    expect((srLabel.textContent ?? '').trim().length).toBeGreaterThan(0);
    expect(srLabel.textContent).toContain('4,5');
    expect(srLabel.textContent).toContain('42');
  });

  it('uses a "no rating yet" screen-reader phrase when the venue is unrated', () => {
    const fixture = render(baseVm({ rating: NO_RATING }));
    const srLabel = fixture.nativeElement.querySelector(
      '[data-testid="result-rating"] .dp-sr-only',
    );
    expect(srLabel).not.toBeNull();
    expect((srLabel.textContent ?? '').trim().length).toBeGreaterThan(0);
  });

  it('shows distance when present and omits it when absent', () => {
    const withGeo = render(baseVm({ distance: '1,2 km' }));
    expect(text(withGeo, 'result-distance')).toContain('1,2 km');

    const withoutGeo = render(baseVm({ distance: null }));
    expect(text(withoutGeo, 'result-distance')).toBeNull();
  });

  it('renders coverage as secondary "N of M" info', () => {
    const fixture = render(baseVm({ coverageCovered: 2, coverageTotal: 3 }));
    const coverage = text(fixture, 'result-coverage');
    expect(coverage).toContain('2');
    expect(coverage).toContain('3');
    // Coverage lives in a de-emphasised caption, separate from the name/price (invariant #5).
    const el = fixture.nativeElement.querySelector('[data-testid="result-coverage"]');
    expect(el.classList.contains('dp-card__coverage')).toBe(true);
  });

  it('hides coverage when there is no selection (total 0)', () => {
    const fixture = render(baseVm({ coverageTotal: 0 }));
    expect(text(fixture, 'result-coverage')).toBeNull();
  });

  it('renders the generic photo by default and the real photo only when permitted', () => {
    const generic = render(baseVm({ photo: { isReal: false, src: 'generic-dish.svg', alt: 'X' } }));
    expect(generic.nativeElement.querySelector('[data-testid="generic-photo"]')).not.toBeNull();
    expect(generic.nativeElement.querySelector('[data-testid="real-photo"]')).toBeNull();

    const real = render(
      baseVm({ photo: { isReal: true, src: 'https://cdn/real.jpg', alt: 'Real dish' } }),
    );
    expect(real.nativeElement.querySelector('[data-testid="real-photo"]')).not.toBeNull();
    expect(real.nativeElement.querySelector('[data-testid="generic-photo"]')).toBeNull();
  });

  it('emits open with the view-model on the open affordance', () => {
    const vm = baseVm();
    const fixture = render(vm);
    let opened: ResultCardViewModel | undefined;
    fixture.componentInstance.open.subscribe((v) => (opened = v));

    (
      fixture.nativeElement.querySelector('[data-testid="result-open"]') as HTMLButtonElement
    ).click();
    expect(opened).toBe(vm);
  });

  it('renders the inline "View on map" affordance with an accessible label (§5.7)', () => {
    const fixture = render(baseVm({ name: 'Borscht House' }));
    const btn = fixture.nativeElement.querySelector(
      '[data-testid="result-view-map"]',
    ) as HTMLButtonElement | null;
    expect(btn).not.toBeNull();
    expect(btn?.getAttribute('aria-label')).toContain('Borscht House');
  });

  it('emits viewMap (not open) when the "View on map" affordance is tapped', () => {
    const vm = baseVm();
    const fixture = render(vm);
    let mapVm: ResultCardViewModel | undefined;
    let opened = false;
    fixture.componentInstance.viewMap.subscribe((v) => (mapVm = v));
    fixture.componentInstance.open.subscribe(() => (opened = true));

    (
      fixture.nativeElement.querySelector('[data-testid="result-view-map"]') as HTMLButtonElement
    ).click();
    expect(mapVm).toBe(vm);
    // The map flow must not navigate into details (no `open` emission).
    expect(opened).toBe(false);
  });
});
