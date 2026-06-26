import { TestBed, type ComponentFixture } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';

import { PrefixSearchComponent, type PrefixSearchResult } from './prefix-search.component';

const RESULTS: readonly PrefixSearchResult[] = [
  { id: 'c-1', label: 'Фаст-фуд', kind: 'category', selected: false },
  { id: 'd-1', label: 'Борщ', kind: 'dish', selected: false },
];

describe('PrefixSearchComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [PrefixSearchComponent],
      providers: [provideNoopAnimations()],
    }).compileComponents();
  });

  function createWith(
    inputs: Record<string, unknown> = {},
  ): ComponentFixture<PrefixSearchComponent> {
    const fixture = TestBed.createComponent(PrefixSearchComponent);
    for (const [name, value] of Object.entries(inputs)) {
      fixture.componentRef.setInput(name, value);
    }
    fixture.detectChanges();
    return fixture;
  }

  function input(fixture: ComponentFixture<PrefixSearchComponent>): HTMLInputElement {
    return fixture.nativeElement.querySelector(
      '[data-testid="prefix-search-input"]',
    ) as HTMLInputElement;
  }

  it('emits the raw query text on input (the container debounces — no engine free text)', () => {
    const fixture = createWith();
    const queries: string[] = [];
    fixture.componentInstance.query.subscribe((q) => queries.push(q));
    const box = input(fixture);
    box.value = 'бор';
    box.dispatchEvent(new Event('input'));
    expect(queries).toEqual(['бор']);
  });

  it('renders the result rows and emits pick when a row is clicked', () => {
    const fixture = createWith({ results: RESULTS, queried: true });
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelectorAll('[data-testid^="prefix-search-result-"]').length).toBe(2);

    let picked: PrefixSearchResult | undefined;
    fixture.componentInstance.pick.subscribe((r) => (picked = r));
    (el.querySelector('[data-testid="prefix-search-result-d-1"]') as HTMLElement).click();
    expect(picked).toEqual(RESULTS[1]);
  });

  it('navigates with the keyboard (ArrowDown then Enter picks the active row)', () => {
    const fixture = createWith({ results: RESULTS, queried: true });
    let picked: PrefixSearchResult | undefined;
    fixture.componentInstance.pick.subscribe((r) => (picked = r));
    const box = input(fixture);
    box.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowDown' }));
    box.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter' }));
    expect(picked).toEqual(RESULTS[0]);
  });

  it('resets the active descendant when a new result set arrives (no dangling aria-activedescendant)', () => {
    const fixture = createWith({ results: RESULTS, queried: true });
    const box = input(fixture);
    box.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowDown' }));
    fixture.detectChanges();
    expect(box.getAttribute('aria-activedescendant')).toBe('dp-search-option-0');

    // A fresh search lands with a different result set; the active row must not dangle.
    fixture.componentRef.setInput('results', [
      { id: 'd-9', label: 'Піца', kind: 'dish', selected: false },
    ]);
    fixture.detectChanges();
    expect(box.getAttribute('aria-activedescendant')).toBeNull();
  });

  it('exposes combobox/listbox ARIA wiring for screen readers (WCAG)', () => {
    const el = createWith({ results: RESULTS, queried: true }).nativeElement as HTMLElement;
    const box = el.querySelector('[data-testid="prefix-search-input"]');
    expect(box?.getAttribute('role')).toBe('combobox');
    expect(box?.getAttribute('aria-controls')).toBe('dp-search-results');
    expect(el.querySelector('[role="listbox"]')).not.toBeNull();
    expect(el.querySelectorAll('[role="option"]').length).toBe(2);
  });

  it('shows the empty state only after a prefix was queried with no matches', () => {
    const queriedEmpty = createWith({ results: [], queried: true }).nativeElement as HTMLElement;
    expect(queriedEmpty.querySelector('app-error-state')).not.toBeNull();

    const notQueried = createWith({ results: [], queried: false }).nativeElement as HTMLElement;
    expect(notQueried.querySelector('app-error-state')).toBeNull();
  });

  it('renders the loading state while a lookup is in flight', () => {
    const el = createWith({ loading: true }).nativeElement as HTMLElement;
    expect(el.querySelector('app-loading')).not.toBeNull();
  });

  it('clears the input and emits an empty query (deterministic reset)', () => {
    const fixture = createWith();
    const queries: string[] = [];
    const box = input(fixture);
    box.value = 'бор';
    box.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    fixture.componentInstance.query.subscribe((q) => queries.push(q));
    (
      fixture.nativeElement.querySelector(
        '[data-testid="prefix-search-clear"]',
      ) as HTMLButtonElement
    ).click();
    expect(queries).toContain('');
  });
});
