import { TestBed, type ComponentFixture } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import type { MatchMode } from 'core';

import { MatchControlComponent } from './match-control.component';

describe('MatchControlComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [MatchControlComponent],
      providers: [provideNoopAnimations()],
    }).compileComponents();
  });

  function createWith(value: MatchMode): ComponentFixture<MatchControlComponent> {
    const fixture = TestBed.createComponent(MatchControlComponent);
    fixture.componentRef.setInput('value', value);
    fixture.detectChanges();
    return fixture;
  }

  it('emits the chosen match mode when the And toggle is selected', () => {
    const fixture = createWith('or');
    let emitted: MatchMode | undefined;
    fixture.componentInstance.modeChange.subscribe((m) => (emitted = m));

    (
      fixture.nativeElement.querySelector('[data-testid="match-and"] button') as HTMLButtonElement
    ).click();
    expect(emitted).toBe('and');
  });

  // The hint assertions below check the resolved Ukrainian copy (not the raw catalog key).
  // That works because the `TranslateService` is supplied GLOBALLY by
  // `projects/app/src/test-providers.ts` (wired via `providersFile` in angular.json) — the
  // in-memory loader + `uk` catalog — so this spec deliberately does not register translation
  // providers locally and still renders translated text.
  it('labels And as a combo (all selected present) in the helper hint', () => {
    const el = createWith('and').nativeElement as HTMLElement;
    const hint = el.querySelector('[data-testid="match-hint"]') as HTMLElement;
    expect(hint.textContent).toContain('всі обрані');
  });

  it('explains Or as at-least-one when Or is active', () => {
    const el = createWith('or').nativeElement as HTMLElement;
    const hint = el.querySelector('[data-testid="match-hint"]') as HTMLElement;
    expect(hint.textContent).toContain('хоча б одну');
  });
});
