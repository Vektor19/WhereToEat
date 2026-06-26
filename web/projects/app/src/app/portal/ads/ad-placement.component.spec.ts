import { TestBed, type ComponentFixture } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of, throwError, type Observable } from 'rxjs';
import {
  CREATE_AD_PLACEMENT,
  type ApiError,
  type CreateAdPlacementBody,
  type CreatedAdPlacement,
} from 'core';

import { AdPlacementComponent } from './ad-placement.component';

const ERROR: ApiError = {
  kind: 'validation',
  status: 400,
  code: 'BadRequest',
  message: 'bad',
  messageKey: 'errors.validation',
};

describe('AdPlacementComponent', () => {
  let calls: { id: string; body: CreateAdPlacementBody }[];
  let result: () => Observable<CreatedAdPlacement>;

  beforeEach(async () => {
    calls = [];
    result = () => of({ id: 'ad-1' });

    await TestBed.configureTestingModule({
      imports: [AdPlacementComponent],
      providers: [
        provideNoopAnimations(),
        {
          provide: CREATE_AD_PLACEMENT,
          useValue: {
            execute: (id: string, body: CreateAdPlacementBody) => {
              calls.push({ id, body });
              return result();
            },
          },
        },
      ],
    }).compileComponents();
  });

  function render(): ComponentFixture<AdPlacementComponent> {
    const fixture = TestBed.createComponent(AdPlacementComponent);
    fixture.detectChanges();
    return fixture;
  }

  function el(fixture: ComponentFixture<AdPlacementComponent>, testid: string): Element | null {
    return fixture.nativeElement.querySelector(`[data-testid="${testid}"]`);
  }

  function fill(
    fixture: ComponentFixture<AdPlacementComponent>,
    values: { venueId?: string; targetingKey?: string; startsAt?: string; endsAt?: string },
  ): void {
    const { controls } = fixture.componentInstance.form;
    controls['venueId'].setValue(values.venueId ?? 'v-1');
    controls['targetingKey'].setValue(values.targetingKey ?? 'pizza');
    controls['startsAt'].setValue(values.startsAt ?? '2026-07-01T10:00');
    controls['endsAt'].setValue(values.endsAt ?? '2026-07-31T10:00');
    fixture.detectChanges();
  }

  function submit(fixture: ComponentFixture<AdPlacementComponent>): void {
    (el(fixture, 'ad-form') as HTMLFormElement).dispatchEvent(new Event('submit'));
    fixture.detectChanges();
  }

  it('creates a placement with the {targetingKey, startsAt, endsAt} body and reflects 201 + id', () => {
    const fixture = render();
    fill(fixture, {});
    submit(fixture);

    expect(calls.length).toBe(1);
    expect(calls[0].id).toBe('v-1');
    expect(calls[0].body.targetingKey).toBe('pizza');
    expect(calls[0].body.startsAt).toBe(new Date('2026-07-01T10:00').toISOString());
    expect(calls[0].body.endsAt).toBe(new Date('2026-07-31T10:00').toISOString());

    expect(el(fixture, 'ad-created')).not.toBeNull();
    expect((el(fixture, 'ad-created-id') as Element).textContent).toBe('ad-1');
  });

  it('maps startsAt/endsAt to ISO-8601 strings (start before end)', () => {
    const fixture = render();
    fill(fixture, { startsAt: '2026-01-02T08:30', endsAt: '2026-01-09T08:30' });
    submit(fixture);

    const { body } = calls[0];
    expect(new Date(body.startsAt).getTime()).toBeLessThan(new Date(body.endsAt).getTime());
    expect(body.startsAt).toBe(new Date('2026-01-02T08:30').toISOString());
  });

  it('rejects a window where startsAt >= endsAt (form invalid, no call)', () => {
    const fixture = render();
    fill(fixture, { startsAt: '2026-07-31T10:00', endsAt: '2026-07-01T10:00' });
    expect(fixture.componentInstance.form.invalid).toBe(true);
    expect(el(fixture, 'ad-range-error')).not.toBeNull();

    submit(fixture);
    expect(calls.length).toBe(0);
  });

  it('rejects an equal start/end window', () => {
    const fixture = render();
    fill(fixture, { startsAt: '2026-07-01T10:00', endsAt: '2026-07-01T10:00' });
    expect(fixture.componentInstance.form.invalid).toBe(true);
    submit(fixture);
    expect(calls.length).toBe(0);
  });

  it('does not submit when required fields are missing', () => {
    const fixture = render();
    fill(fixture, { targetingKey: '' });
    submit(fixture);
    expect(calls.length).toBe(0);
  });

  it('surfaces the typed error from a failed create', () => {
    const fixture = render();
    fill(fixture, {});
    result = () => throwError(() => ERROR);
    submit(fixture);
    expect(el(fixture, 'save-error')).not.toBeNull();
    expect(el(fixture, 'ad-created')).toBeNull();
  });

  it('frames the slot as a paid, labeled placement separate from organic ranking (invariant #10)', () => {
    const fixture = render();
    const note = el(fixture, 'ad-labeled-note');
    expect(note).not.toBeNull();
    const text = (note as Element).textContent ?? '';
    expect(text).toContain('платний рекламний слот');
    expect(text).toContain('«Реклама»');
    expect(text).toContain('Органічний рейтинг не продається');
  });
});
