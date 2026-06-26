import { TestBed, type ComponentFixture } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of, throwError, type Observable } from 'rxjs';
import { GRANT_VERIFIED, REVOKE_VERIFIED, type ApiError, type VerifiedTier } from 'core';

import { VerifiedComponent } from './verified.component';

const ERROR: ApiError = {
  kind: 'not-found',
  status: 404,
  code: 'NotFound',
  message: 'missing',
  messageKey: 'errors.notFound',
};

describe('VerifiedComponent', () => {
  let grantCalls: { id: string; tier: VerifiedTier }[];
  let revokeCalls: string[];
  let grantResult: () => Observable<void>;
  let revokeResult: () => Observable<void>;

  beforeEach(async () => {
    grantCalls = [];
    revokeCalls = [];
    grantResult = () => of(undefined);
    revokeResult = () => of(undefined);

    await TestBed.configureTestingModule({
      imports: [VerifiedComponent],
      providers: [
        provideNoopAnimations(),
        {
          provide: GRANT_VERIFIED,
          useValue: {
            execute: (id: string, tier: VerifiedTier) => {
              grantCalls.push({ id, tier });
              return grantResult();
            },
          },
        },
        {
          provide: REVOKE_VERIFIED,
          useValue: {
            execute: (id: string) => {
              revokeCalls.push(id);
              return revokeResult();
            },
          },
        },
      ],
    }).compileComponents();
  });

  function render(): ComponentFixture<VerifiedComponent> {
    const fixture = TestBed.createComponent(VerifiedComponent);
    fixture.detectChanges();
    return fixture;
  }

  function el(fixture: ComponentFixture<VerifiedComponent>, testid: string): Element | null {
    return fixture.nativeElement.querySelector(`[data-testid="${testid}"]`);
  }

  function setVenue(fixture: ComponentFixture<VerifiedComponent>, id: string): void {
    fixture.componentInstance.form.controls['venueId'].setValue(id);
    fixture.detectChanges();
  }

  it('grants the default Basic tier (ordinal mapping happens in the data-access port)', () => {
    const fixture = render();
    setVenue(fixture, 'v-1');
    (el(fixture, 'verified-grant') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(grantCalls).toEqual([{ id: 'v-1', tier: 'Basic' }]);
    expect(el(fixture, 'save-success')).not.toBeNull();
  });

  it('grants the chosen Pro tier', () => {
    const fixture = render();
    setVenue(fixture, 'v-2');
    fixture.componentInstance.form.controls['tier'].setValue('Pro');
    fixture.detectChanges();

    (el(fixture, 'verified-grant') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(grantCalls).toEqual([{ id: 'v-2', tier: 'Pro' }]);
  });

  it('does not grant without a venue id (button disabled / form invalid)', () => {
    const fixture = render();
    (el(fixture, 'verified-grant') as HTMLButtonElement).click();
    fixture.detectChanges();
    expect(grantCalls.length).toBe(0);
  });

  it('revokes the venue (DELETE) and reflects 204 success', () => {
    const fixture = render();
    setVenue(fixture, 'v-3');
    (el(fixture, 'verified-revoke') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(revokeCalls).toEqual(['v-3']);
    expect(grantCalls.length).toBe(0);
    expect(el(fixture, 'save-success')).not.toBeNull();
  });

  it('does not revoke without a venue id', () => {
    const fixture = render();
    fixture.componentInstance.revoke();
    fixture.detectChanges();
    expect(revokeCalls.length).toBe(0);
  });

  it('surfaces the typed error from a failed grant', () => {
    const fixture = render();
    setVenue(fixture, 'v-1');
    grantResult = () => throwError(() => ERROR);
    (el(fixture, 'verified-grant') as HTMLButtonElement).click();
    fixture.detectChanges();
    expect(el(fixture, 'save-error')).not.toBeNull();
  });

  it('frames the Verified badge as partner status, not a quality seal (invariant #10 / §7.1)', () => {
    const fixture = render();
    const note = el(fixture, 'verified-partner-note');
    expect(note).not.toBeNull();
    const text = (note as Element).textContent ?? '';
    // The copy must communicate partner/confirmed status and explicitly deny "quality seal from us".
    expect(text).toContain('партнерського');
    expect(text).toContain('не «знак якості від нас»');
  });
});
