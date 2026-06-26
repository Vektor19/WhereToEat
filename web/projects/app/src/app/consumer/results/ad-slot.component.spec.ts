import { TestBed, type ComponentFixture } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';

import { AdSlotComponent } from './ad-slot.component';
import type { ResultCardViewModel } from './result-card.view-model';

const SPONSORED_VM: ResultCardViewModel = {
  restaurantId: 'ad-1',
  name: 'Партнерська піцерія',
  position: 1,
  basketPrice: '199,00 ₴',
  rating: { hasRating: true, value: '4,2', count: 30, isLowConfidence: false, noteKey: null },
  distance: null,
  coverageCovered: 1,
  coverageTotal: 2,
  photo: { isReal: false, src: 'generic-dish.svg', alt: 'Партнерська піцерія' },
  isAd: true,
};

describe('AdSlotComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AdSlotComponent],
      providers: [provideNoopAnimations()],
    }).compileComponents();
  });

  function render(): ComponentFixture<AdSlotComponent> {
    const fixture = TestBed.createComponent(AdSlotComponent);
    fixture.componentRef.setInput('vm', SPONSORED_VM);
    fixture.detectChanges();
    return fixture;
  }

  it('renders a clearly labeled, visually-separated ad slot (invariant #10)', () => {
    const fixture = render();
    const slot = fixture.nativeElement.querySelector('[data-testid="ad-slot"]');
    expect(slot).not.toBeNull();

    // The "Реклама" assertions below check resolved Ukrainian copy (not the raw catalog key). That
    // works because the `TranslateService` is supplied GLOBALLY by
    // `projects/app/src/test-providers.ts` (wired via `providersFile` in angular.json) — the
    // in-memory loader + `uk` catalog — so this spec deliberately does not register translation
    // providers locally and still renders translated text.
    // Labeled "Реклама/Промо".
    const label = fixture.nativeElement.querySelector('[data-testid="ad-label"]');
    expect(label).not.toBeNull();
    expect((label.textContent ?? '').trim()).toContain('Реклама');

    // Visually separated: the slot is its own bordered/tinted container around the card.
    expect(slot.classList.contains('dp-ad')).toBe(true);
    // The labeled section is an accessible landmark naming the ad.
    expect(slot.getAttribute('aria-label')).toContain('Реклама');
  });

  it('still renders the underlying result card inside the slot', () => {
    const fixture = render();
    expect(fixture.nativeElement.querySelector('[data-testid="result-card-ad-1"]')).not.toBeNull();
  });

  it('re-emits open from the wrapped card', () => {
    const fixture = render();
    let opened: ResultCardViewModel | undefined;
    fixture.componentInstance.open.subscribe((v) => (opened = v));

    (
      fixture.nativeElement.querySelector('[data-testid="result-open"]') as HTMLButtonElement
    ).click();
    expect(opened).toBe(SPONSORED_VM);
  });
});
