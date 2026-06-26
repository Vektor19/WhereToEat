import { TestBed, type ComponentFixture } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import type { FilterKey, FilterSelection } from 'core';

import {
  DEFAULT_FILTER_DESCRIPTORS,
  FiltersControlComponent,
  type FilterDescriptor,
} from './filters-control.component';

describe('FiltersControlComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [FiltersControlComponent],
      providers: [provideNoopAnimations()],
    }).compileComponents();
  });

  function createWith(
    inputs: Record<string, unknown> = {},
  ): ComponentFixture<FiltersControlComponent> {
    const fixture = TestBed.createComponent(FiltersControlComponent);
    for (const [name, value] of Object.entries(inputs)) {
      fixture.componentRef.setInput(name, value);
    }
    fixture.detectChanges();
    return fixture;
  }

  function typeInto(
    fixture: ComponentFixture<FiltersControlComponent>,
    key: FilterKey,
    value: string,
  ): void {
    const input = fixture.nativeElement.querySelector(
      `[data-testid="filter-input-${key}"]`,
    ) as HTMLInputElement;
    input.value = value;
    input.dispatchEvent(new Event('input'));
  }

  it('renders the default price (max) and rating (min) filters', () => {
    const el = createWith().nativeElement as HTMLElement;
    expect(el.querySelector('[data-testid="filter-price"]')).not.toBeNull();
    expect(el.querySelector('[data-testid="filter-rating"]')).not.toBeNull();
  });

  it('emits apply with the key and value when a numeric value is entered', () => {
    const fixture = createWith();
    let applied: { key: FilterKey; value: string } | undefined;
    fixture.componentInstance.apply.subscribe((e) => (applied = e));
    typeInto(fixture, 'price', '150');
    expect(applied).toEqual({ key: 'price', value: '150' });
  });

  it('emits clear when the input is emptied', () => {
    const fixture = createWith();
    let cleared: FilterKey | undefined;
    fixture.componentInstance.clear.subscribe((k) => (cleared = k));
    typeInto(fixture, 'rating', '');
    expect(cleared).toBe('rating');
  });

  it('reflects an applied filter and offers a per-row clear button', () => {
    const applied: readonly FilterSelection[] = [{ key: 'price', value: '200' }];
    const fixture = createWith({ applied });
    const input = fixture.nativeElement.querySelector(
      '[data-testid="filter-input-price"]',
    ) as HTMLInputElement;
    expect(input.value).toBe('200');

    let cleared: FilterKey | undefined;
    fixture.componentInstance.clear.subscribe((k) => (cleared = k));
    (
      fixture.nativeElement.querySelector('[data-testid="filter-clear-price"]') as HTMLButtonElement
    ).click();
    expect(cleared).toBe('price');
  });

  it('renders a new filter key from a longer descriptor list with no code change', () => {
    // A hypothetical additive descriptor with a distinct (future) contract key proves the renderer is
    // purely data-driven (invariant #4) — adding a row is data, not template/logic. The key is cast
    // because the contract only ships `price`/`rating` today; a real new filter would widen FilterKey.
    const descriptors: readonly FilterDescriptor[] = [
      ...DEFAULT_FILTER_DESCRIPTORS,
      {
        key: 'open-now' as FilterKey,
        labelKey: 'consumer.filters.maxPrice',
        icon: 'tune',
        kind: 'numeric',
      },
    ];
    const el = createWith({ descriptors }).nativeElement as HTMLElement;
    expect(el.querySelectorAll('[data-testid^="filter-input-"]').length).toBe(descriptors.length);
  });
});
