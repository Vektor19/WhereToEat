import { TestBed, type ComponentFixture } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import type { ApiError } from 'core';

import { ErrorStateComponent } from './error-state.component';

function serverError(): ApiError {
  return {
    kind: 'server',
    status: 500,
    code: 'Http.ServerError',
    message: 'boom',
    messageKey: 'errors.server',
  };
}

describe('ErrorStateComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ErrorStateComponent],
      providers: [provideNoopAnimations()],
    }).compileComponents();
  });

  function createWith(inputs: Record<string, unknown>): ComponentFixture<ErrorStateComponent> {
    const fixture = TestBed.createComponent(ErrorStateComponent);
    for (const [name, value] of Object.entries(inputs)) {
      fixture.componentRef.setInput(name, value);
    }
    fixture.detectChanges();
    return fixture;
  }

  it('renders the localized fallback copy for the error messageKey', () => {
    const el = createWith({ error: serverError() }).nativeElement as HTMLElement;
    const msg = el.querySelector('.dp-error-state__message')?.textContent ?? '';
    expect(msg).toContain('сервері');
    expect(el.querySelector('[data-testid="error-retry"]')).not.toBeNull();
  });

  it('renders the empty-state copy and no retry when there is no error', () => {
    const el = createWith({ emptyMessage: 'Порожньо.' }).nativeElement as HTMLElement;
    expect(el.querySelector('.dp-error-state__message')?.textContent).toContain('Порожньо.');
    expect(el.querySelector('[data-testid="error-retry"]')).toBeNull();
  });

  it('emits retry when the button is clicked', () => {
    const fixture = createWith({ error: serverError() });
    let emitted = false;
    fixture.componentInstance.retry.subscribe(() => (emitted = true));
    const btn = fixture.nativeElement.querySelector(
      '[data-testid="error-retry"]',
    ) as HTMLButtonElement;
    btn.click();
    expect(emitted).toBe(true);
  });

  it('hides retry when retryable is false even with an error', () => {
    const el = createWith({ error: serverError(), retryable: false }).nativeElement as HTMLElement;
    expect(el.querySelector('[data-testid="error-retry"]')).toBeNull();
  });
});
