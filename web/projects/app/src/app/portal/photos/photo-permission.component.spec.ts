import { TestBed, type ComponentFixture } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of, throwError, type Observable } from 'rxjs';
import { SET_PHOTO_PERMISSION, type ApiError } from 'core';

import { PhotoPermissionComponent } from './photo-permission.component';

const ERROR: ApiError = {
  kind: 'not-found',
  status: 404,
  code: 'NotFound',
  message: 'missing',
  messageKey: 'errors.notFound',
};

describe('PhotoPermissionComponent', () => {
  let calls: { id: string; value: boolean }[];
  let result: () => Observable<void>;

  beforeEach(async () => {
    calls = [];
    result = () => of(undefined);

    await TestBed.configureTestingModule({
      imports: [PhotoPermissionComponent],
      providers: [
        provideNoopAnimations(),
        {
          provide: SET_PHOTO_PERMISSION,
          useValue: {
            execute: (id: string, value: boolean) => {
              calls.push({ id, value });
              return result();
            },
          },
        },
      ],
    }).compileComponents();
  });

  function render(): ComponentFixture<PhotoPermissionComponent> {
    const fixture = TestBed.createComponent(PhotoPermissionComponent);
    fixture.detectChanges();
    return fixture;
  }

  function el(fixture: ComponentFixture<PhotoPermissionComponent>, testid: string): Element | null {
    return fixture.nativeElement.querySelector(`[data-testid="${testid}"]`);
  }

  function setPhotoId(fixture: ComponentFixture<PhotoPermissionComponent>, id: string): void {
    const input = el(fixture, 'photo-id-input') as HTMLInputElement;
    input.value = id;
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
  }

  it('does not submit without a photo id (form invalid)', () => {
    const fixture = render();
    (el(fixture, 'photo-form') as HTMLFormElement).dispatchEvent(new Event('submit'));
    fixture.detectChanges();
    expect(calls.length).toBe(0);
  });

  it('toggles the permission gate with the {value} body (204 success)', () => {
    const fixture = render();
    setPhotoId(fixture, 'p-1');
    fixture.componentInstance.form.controls['value'].setValue(true);
    fixture.detectChanges();

    (el(fixture, 'photo-form') as HTMLFormElement).dispatchEvent(new Event('submit'));
    fixture.detectChanges();

    expect(calls).toEqual([{ id: 'p-1', value: true }]);
    expect(el(fixture, 'save-success')).not.toBeNull();
  });

  it('defaults the permission off (generic stays the default — invariant #8)', () => {
    const fixture = render();
    setPhotoId(fixture, 'p-1');
    (el(fixture, 'photo-form') as HTMLFormElement).dispatchEvent(new Event('submit'));
    fixture.detectChanges();
    expect(calls).toEqual([{ id: 'p-1', value: false }]);
  });

  it('surfaces the typed error from a failed write', () => {
    const fixture = render();
    setPhotoId(fixture, 'p-1');
    result = () => throwError(() => ERROR);
    (el(fixture, 'photo-form') as HTMLFormElement).dispatchEvent(new Event('submit'));
    fixture.detectChanges();
    expect(el(fixture, 'save-error')).not.toBeNull();
  });
});
