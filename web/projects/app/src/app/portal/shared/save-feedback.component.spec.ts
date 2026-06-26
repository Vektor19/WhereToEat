import { TestBed, type ComponentFixture } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import type { ApiError } from 'core';

import { SaveFeedbackComponent } from './save-feedback.component';

const ERROR: ApiError = {
  kind: 'not-found',
  status: 404,
  code: 'NotFound',
  message: 'missing',
  messageKey: 'errors.notFound',
};

describe('SaveFeedbackComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SaveFeedbackComponent],
      providers: [provideNoopAnimations()],
    }).compileComponents();
  });

  function render(inputs: Record<string, unknown>): ComponentFixture<SaveFeedbackComponent> {
    const fixture = TestBed.createComponent(SaveFeedbackComponent);
    for (const [name, value] of Object.entries(inputs)) {
      fixture.componentRef.setInput(name, value);
    }
    fixture.detectChanges();
    return fixture;
  }

  function el(fixture: ComponentFixture<SaveFeedbackComponent>, testid: string): Element | null {
    return fixture.nativeElement.querySelector(`[data-testid="${testid}"]`);
  }

  it('shows the pending strip while saving', () => {
    const fixture = render({ saving: true });
    expect(el(fixture, 'save-pending')).not.toBeNull();
    expect(el(fixture, 'save-success')).toBeNull();
  });

  it('shows the success strip when saved', () => {
    const fixture = render({ saved: true });
    expect(el(fixture, 'save-success')).not.toBeNull();
  });

  it('shows the localized error strip (role=alert) from the typed error', () => {
    const fixture = render({ error: ERROR });
    const err = el(fixture, 'save-error');
    expect(err).not.toBeNull();
    expect(err?.getAttribute('role')).toBe('alert');
    expect(err?.querySelector('[data-message-key]')?.getAttribute('data-message-key')).toBe(
      'errors.notFound',
    );
  });

  it('prefers the error over the saved state', () => {
    const fixture = render({ saved: true, error: ERROR });
    expect(el(fixture, 'save-error')).not.toBeNull();
    expect(el(fixture, 'save-success')).toBeNull();
  });
});
