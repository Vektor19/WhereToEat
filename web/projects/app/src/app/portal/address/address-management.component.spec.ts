import { TestBed, type ComponentFixture } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { Subject, of, throwError, type Observable } from 'rxjs';
import {
  EDIT_ADDRESS,
  GET_RESTAURANT_DETAILS,
  type ApiError,
  type EditAddressBody,
  type RestaurantDetailsDto,
} from 'core';

import { AddressManagementComponent } from './address-management.component';

function details(over: Partial<RestaurantDetailsDto> = {}): RestaurantDetailsDto {
  return {
    id: 'r-1',
    name: 'Тест',
    addressLine: 'вул. Хрещатик, 1',
    addressCity: 'Київ',
    contactLinks: [],
    menuItems: [],
    ...over,
  };
}

const ERROR: ApiError = {
  kind: 'validation',
  status: 400,
  code: 'Validation',
  message: 'bad',
  messageKey: 'errors.validation',
};

describe('AddressManagementComponent', () => {
  let details$: Subject<RestaurantDetailsDto>;
  let editCalls: { id: string; body: EditAddressBody }[];
  let editResult: () => Observable<void>;

  beforeEach(async () => {
    details$ = new Subject<RestaurantDetailsDto>();
    editCalls = [];
    editResult = () => of(undefined);

    await TestBed.configureTestingModule({
      imports: [AddressManagementComponent],
      providers: [
        provideNoopAnimations(),
        { provide: GET_RESTAURANT_DETAILS, useValue: { execute: () => details$.asObservable() } },
        {
          provide: EDIT_ADDRESS,
          useValue: {
            execute: (id: string, body: EditAddressBody) => {
              editCalls.push({ id, body });
              return editResult();
            },
          },
        },
      ],
    }).compileComponents();
  });

  function render(): ComponentFixture<AddressManagementComponent> {
    const fixture = TestBed.createComponent(AddressManagementComponent);
    fixture.detectChanges();
    return fixture;
  }

  function el(
    fixture: ComponentFixture<AddressManagementComponent>,
    testid: string,
  ): Element | null {
    return fixture.nativeElement.querySelector(`[data-testid="${testid}"]`);
  }

  function load(fixture: ComponentFixture<AddressManagementComponent>, dto = details()): void {
    fixture.componentInstance.loadRestaurant('r-1');
    fixture.detectChanges();
    details$.next(dto);
    details$.complete();
    fixture.detectChanges();
  }

  it('pre-fills the form from the loaded address', () => {
    const fixture = render();
    load(fixture);
    expect((el(fixture, 'address-line-input') as HTMLInputElement).value).toBe('вул. Хрещатик, 1');
    expect((el(fixture, 'address-city-input') as HTMLInputElement).value).toBe('Київ');
  });

  it('saves the address line + city (204 success)', () => {
    const fixture = render();
    load(fixture);
    const line = el(fixture, 'address-line-input') as HTMLInputElement;
    line.value = 'вул. Лесі Українки, 5';
    line.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    (el(fixture, 'address-form') as HTMLFormElement).dispatchEvent(new Event('submit'));
    fixture.detectChanges();

    expect(editCalls).toEqual([
      { id: 'r-1', body: { addressLine: 'вул. Лесі Українки, 5', city: 'Київ' } },
    ]);
    expect(el(fixture, 'save-success')).not.toBeNull();
  });

  it('sends city as null when the city field is blank', () => {
    const fixture = render();
    load(fixture, details({ addressCity: null }));
    (el(fixture, 'address-form') as HTMLFormElement).dispatchEvent(new Event('submit'));
    fixture.detectChanges();
    expect(editCalls[0].body).toEqual({ addressLine: 'вул. Хрещатик, 1', city: null });
  });

  it('shows the re-geocode note (invariant #7)', () => {
    const fixture = render();
    load(fixture);
    const text = fixture.nativeElement.textContent ?? '';
    expect(text).toContain('геокодування');
  });

  it('surfaces the typed error from a failed save', () => {
    const fixture = render();
    load(fixture);
    editResult = () => throwError(() => ERROR);
    (el(fixture, 'address-form') as HTMLFormElement).dispatchEvent(new Event('submit'));
    fixture.detectChanges();
    expect(el(fixture, 'save-error')).not.toBeNull();
  });

  it('renders the typed error state on a 404 and no form', () => {
    const fixture = render();
    fixture.componentInstance.loadRestaurant('r-1');
    fixture.detectChanges();
    details$.error({
      kind: 'not-found',
      status: 404,
      code: 'NotFound',
      message: 'missing',
      messageKey: 'errors.notFound',
    });
    fixture.detectChanges();
    expect(el(fixture, 'error-state')).not.toBeNull();
    expect(el(fixture, 'address-form')).toBeNull();
  });
});
