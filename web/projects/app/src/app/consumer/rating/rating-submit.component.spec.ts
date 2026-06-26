import { signal } from '@angular/core';
import { TestBed, type ComponentFixture } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { Subject, throwError } from 'rxjs';
import {
  AnalyticsEmitterService,
  AnalyticsEventBuilders,
  AuthService,
  INGEST_ANALYTICS,
  SUBMIT_RATING,
  type ApiError,
  type RawEventBody,
} from 'core';

import { RatingSubmitComponent } from './rating-submit.component';

/** A controllable {@link SUBMIT_RATING} port: tests resolve/error its `result$` and inspect the call. */
class StubSubmitRating {
  result$ = new Subject<void>();
  calls: { restaurantId: string; score: number }[] = [];
  fail: ApiError | null = null;

  execute(restaurantId: string, score: number) {
    this.calls.push({ restaurantId, score });
    if (this.fail) {
      return throwError(() => this.fail);
    }
    return this.result$.asObservable();
  }
}

/** A minimal {@link AuthService} stub with a writable authenticated signal + a spy on login. */
class StubAuthService {
  readonly isAuthenticated = signal(false);
  loginCalls = 0;
  login = (): Promise<void> => {
    this.loginCalls += 1;
    return Promise.resolve();
  };
}

const VALIDATION_ERROR: ApiError = {
  kind: 'validation',
  status: 400,
  code: 'Rating.ScoreOutOfRange',
  message: 'A rating score must be between 1 and 5.',
  messageKey: 'errors.validation',
};

describe('RatingSubmitComponent', () => {
  let submit: StubSubmitRating;
  let auth: StubAuthService;
  let ingestBatches: RawEventBody[][];

  beforeEach(() => {
    TestBed.resetTestingModule();
    submit = new StubSubmitRating();
    auth = new StubAuthService();
    ingestBatches = [];

    TestBed.configureTestingModule({
      imports: [RatingSubmitComponent],
      providers: [
        provideNoopAnimations(),
        AnalyticsEventBuilders,
        { provide: SUBMIT_RATING, useValue: submit },
        { provide: AuthService, useValue: auth },
        {
          provide: INGEST_ANALYTICS,
          useValue: {
            execute: (events: readonly RawEventBody[]) => {
              ingestBatches.push([...events]);
              return new Subject<void>();
            },
          },
        },
      ],
    });
  });

  function render(): ComponentFixture<RatingSubmitComponent> {
    const fixture = TestBed.createComponent(RatingSubmitComponent);
    fixture.componentRef.setInput('restaurantId', 'r-1');
    fixture.detectChanges();
    return fixture;
  }

  function el(fixture: ComponentFixture<RatingSubmitComponent>, testid: string): Element | null {
    return fixture.nativeElement.querySelector(`[data-testid="${testid}"]`);
  }

  function flushAnalytics(): void {
    TestBed.inject(AnalyticsEmitterService).flush();
  }

  it('shows a sign-in prompt (not the star input) when the user is not authenticated', () => {
    const fixture = render();
    expect(el(fixture, 'rating-signin')).not.toBeNull();
    expect(el(fixture, 'rating-stars')).toBeNull();
  });

  it('the sign-in button triggers the AuthService login flow', () => {
    const fixture = render();
    (el(fixture, 'rating-signin-button') as HTMLButtonElement).click();
    expect(auth.loginCalls).toBe(1);
  });

  it('renders an accessible 1..5 star radiogroup once authenticated', () => {
    auth.isAuthenticated.set(true);
    const fixture = render();

    const group = el(fixture, 'rating-stars') as HTMLElement;
    expect(group.getAttribute('role')).toBe('radiogroup');
    const stars = group.querySelectorAll('[role="radio"]') as NodeListOf<HTMLButtonElement>;
    expect(stars.length).toBe(5);
    // Each star is a real focusable button with an accessible name and an aria-checked state.
    stars.forEach((star) => {
      expect(star.tagName).toBe('BUTTON');
      expect((star.getAttribute('aria-label') ?? '').trim().length).toBeGreaterThan(0);
      expect(star.getAttribute('aria-checked')).toBe('false');
    });
  });

  it('submitting posts { score } for the restaurant id and emits rating_given on success', () => {
    auth.isAuthenticated.set(true);
    const fixture = render();

    fixture.componentInstance.select(4);
    fixture.detectChanges();
    (el(fixture, 'rating-submit-button') as HTMLButtonElement).click();

    // The POST is in flight: the data-access port was called with the right path inputs.
    expect(submit.calls).toEqual([{ restaurantId: 'r-1', score: 4 }]);
    // No rating_given yet — it only emits on the success notification.
    flushAnalytics();
    expect(ingestBatches.flat().some((e) => e.kind === 'rating_given')).toBe(false);

    // The 204 success lands.
    submit.result$.next();
    submit.result$.complete();
    fixture.detectChanges();

    expect(el(fixture, 'rating-success')).not.toBeNull();
    flushAnalytics();
    const rated = ingestBatches.flat().filter((e) => e.kind === 'rating_given');
    expect(rated.length).toBe(1);
    expect(rated[0].restaurantId).toBe('r-1');
  });

  it('does not submit when no score is selected (button disabled / guarded)', () => {
    auth.isAuthenticated.set(true);
    const fixture = render();

    fixture.componentInstance.submit();
    expect(submit.calls.length).toBe(0);
  });

  it('surfaces a localized typed error and emits no rating_given on a 400', () => {
    auth.isAuthenticated.set(true);
    submit.fail = VALIDATION_ERROR;
    const fixture = render();

    fixture.componentInstance.select(3);
    fixture.detectChanges();
    (el(fixture, 'rating-submit-button') as HTMLButtonElement).click();
    fixture.detectChanges();

    const error = el(fixture, 'rating-error') as HTMLElement;
    expect(error).not.toBeNull();
    expect(error.getAttribute('data-message-key')).toBe('errors.validation');
    // The localized copy resolved (not a raw key) and is non-empty.
    expect((error.textContent ?? '').trim().length).toBeGreaterThan(0);
    expect(error.textContent ?? '').not.toContain('errors.validation');

    expect(el(fixture, 'rating-success')).toBeNull();
    flushAnalytics();
    expect(ingestBatches.flat().some((e) => e.kind === 'rating_given')).toBe(false);
  });

  it('clears a stale error when a different star is selected after a failed submit', () => {
    auth.isAuthenticated.set(true);
    submit.fail = VALIDATION_ERROR;
    const fixture = render();

    // First attempt fails with a 400 — the error alert is shown.
    fixture.componentInstance.select(3);
    fixture.detectChanges();
    (el(fixture, 'rating-submit-button') as HTMLButtonElement).click();
    fixture.detectChanges();
    expect(el(fixture, 'rating-error')).not.toBeNull();

    // Picking a different star starts a fresh attempt: the stale error is cleared from the DOM.
    fixture.componentInstance.select(5);
    fixture.detectChanges();
    expect(el(fixture, 'rating-error')).toBeNull();
  });

  it('shows the choose-score hint until a star is selected (explains the disabled submit)', () => {
    auth.isAuthenticated.set(true);
    const fixture = render();

    // No score yet → the hint is visible and the submit button is disabled.
    const hint = el(fixture, 'rating-choose-hint') as HTMLElement | null;
    expect(hint).not.toBeNull();
    expect((hint?.textContent ?? '').trim().length).toBeGreaterThan(0);
    expect((el(fixture, 'rating-submit-button') as HTMLButtonElement).disabled).toBe(true);

    // Once a star is picked the hint disappears and submit becomes enabled.
    fixture.componentInstance.select(4);
    fixture.detectChanges();
    expect(el(fixture, 'rating-choose-hint')).toBeNull();
    expect((el(fixture, 'rating-submit-button') as HTMLButtonElement).disabled).toBe(false);
  });

  it('is honest that the displayed rating is our smoothed/cumulative value (invariant #6)', () => {
    auth.isAuthenticated.set(true);
    const fixture = render();
    const note = (el(fixture, 'rating-smoothed-note')?.textContent ?? '').toLowerCase();
    // The copy explains the public figure is our averaged all-time rating, updated on recompute.
    expect(note).toContain('усереднений');
  });
});
