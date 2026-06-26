import { TestBed, type ComponentFixture } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import type { SortMode } from 'core';

import { SortControlComponent } from './sort-control.component';

describe('SortControlComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SortControlComponent],
      providers: [provideNoopAnimations()],
    }).compileComponents();
  });

  function createWith(value: SortMode): ComponentFixture<SortControlComponent> {
    const fixture = TestBed.createComponent(SortControlComponent);
    fixture.componentRef.setInput('value', value);
    fixture.detectChanges();
    return fixture;
  }

  it('offers exactly the five pinned contract sort modes', () => {
    const el = createWith('best').nativeElement as HTMLElement;
    const modes: SortMode[] = ['price', 'distance', 'rating', 'price-quality', 'best'];
    for (const mode of modes) {
      expect(el.querySelector(`[data-testid="sort-${mode}"]`)).not.toBeNull();
    }
    // No extra toggles beyond the five contract modes.
    expect(el.querySelectorAll('mat-button-toggle').length).toBe(5);
  });

  it('emits each selected sort mode', () => {
    const fixture = createWith('best');
    let emitted: SortMode | undefined;
    fixture.componentInstance.modeChange.subscribe((s) => (emitted = s));
    (
      fixture.nativeElement.querySelector('[data-testid="sort-price"] button') as HTMLButtonElement
    ).click();
    expect(emitted).toBe('price');
  });

  // The hint assertions below check resolved Ukrainian copy (not raw catalog keys). That works
  // because the `TranslateService` is supplied GLOBALLY by `projects/app/src/test-providers.ts`
  // (wired via `providersFile` in angular.json) — the in-memory loader + `uk` catalog — so this
  // spec deliberately does not register translation providers locally and still renders uk text.
  it('communicates explicit-sort-dominance with a tie-breaker note for an explicit single-field sort', () => {
    const el = createWith('price').nativeElement as HTMLElement;
    const hint = el.querySelector('[data-testid="sort-hint"]') as HTMLElement;
    expect(hint.getAttribute('data-explicit')).toBe('true');
    // Coverage is presented as secondary/tie-breaker, not the primary ordering.
    expect(hint.textContent).toContain('Покриття');
    expect(hint.textContent).toContain('додатковий критерій');
  });

  it('explains the composed balance (not coverage dominance) for a composite sort', () => {
    const el = createWith('price-quality').nativeElement as HTMLElement;
    const hint = el.querySelector('[data-testid="sort-hint"]') as HTMLElement;
    expect(hint.getAttribute('data-explicit')).toBe('false');
    expect(hint.textContent).toContain('Складений режим');
  });
});
