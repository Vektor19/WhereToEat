import { TestBed, type ComponentFixture } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import type { SelectedItem } from 'core';

import { SelectionSummaryComponent, type SelectionChip } from './selection-summary.component';

function categoryChip(id: string, label: string): SelectionChip {
  return { item: { categoryId: id }, kind: 'category', label };
}

function dishChip(id: string, label: string): SelectionChip {
  return { item: { dishId: id }, kind: 'dish', label };
}

describe('SelectionSummaryComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SelectionSummaryComponent],
      providers: [provideNoopAnimations()],
    }).compileComponents();
  });

  function createWith(
    chips: readonly SelectionChip[],
  ): ComponentFixture<SelectionSummaryComponent> {
    const fixture = TestBed.createComponent(SelectionSummaryComponent);
    fixture.componentRef.setInput('chips', chips);
    fixture.detectChanges();
    return fixture;
  }

  it('renders the empty hint and no clear button when nothing is selected', () => {
    const el = createWith([]).nativeElement as HTMLElement;
    expect(el.querySelector('[data-testid="selection-empty"]')).not.toBeNull();
    expect(el.querySelector('[data-testid="selection-clear"]')).toBeNull();
  });

  it('renders one removable chip per selected item', () => {
    const el = createWith([categoryChip('c-1', 'Фаст-фуд'), dishChip('d-1', 'Борщ')])
      .nativeElement as HTMLElement;
    const chips = el.querySelectorAll('mat-chip');
    expect(chips.length).toBe(2);
    expect(el.textContent).toContain('Фаст-фуд');
    expect(el.textContent).toContain('Борщ');
    expect(el.querySelector('[data-testid="selection-empty"]')).toBeNull();
  });

  it('emits remove with the underlying SelectedItem when a chip remove is clicked', () => {
    const fixture = createWith([dishChip('d-1', 'Борщ')]);
    let removed: SelectedItem | undefined;
    fixture.componentInstance.remove.subscribe((item) => (removed = item));
    const btn = fixture.nativeElement.querySelector(
      '[data-testid="selection-remove-dish-d-1"]',
    ) as HTMLButtonElement;
    btn.click();
    expect(removed).toEqual({ dishId: 'd-1' });
  });

  it('emits clear when the clear-all button is clicked', () => {
    const fixture = createWith([categoryChip('c-1', 'Фаст-фуд')]);
    let cleared = false;
    fixture.componentInstance.clear.subscribe(() => (cleared = true));
    const btn = fixture.nativeElement.querySelector(
      '[data-testid="selection-clear"]',
    ) as HTMLButtonElement;
    btn.click();
    expect(cleared).toBe(true);
  });

  it('labels the region and the chip remove buttons for screen readers (WCAG)', () => {
    const el = createWith([dishChip('d-1', 'Борщ')]).nativeElement as HTMLElement;
    const region = el.querySelector('[data-testid="selection-summary"]');
    expect(region?.getAttribute('aria-label')).toBeTruthy();
    const remove = el.querySelector('[data-testid="selection-remove-dish-d-1"]');
    expect(remove?.getAttribute('aria-label')).toContain('Борщ');
  });
});
