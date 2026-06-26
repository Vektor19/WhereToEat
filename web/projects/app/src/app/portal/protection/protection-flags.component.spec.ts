import { TestBed, type ComponentFixture } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { Subject, of, throwError, type Observable } from 'rxjs';
import {
  GET_RESTAURANT_DETAILS,
  SET_MENU_ITEM_DO_NOT_PARSE,
  SET_RESTAURANT_DO_NOT_UPDATE,
  type ApiError,
  type RestaurantDetailsDto,
} from 'core';

import { ProtectionFlagsComponent } from './protection-flags.component';

function details(): RestaurantDetailsDto {
  return {
    id: 'r-1',
    name: 'Тест',
    addressLine: 'вул. Хрещатик, 1',
    addressCity: 'Київ',
    contactLinks: [],
    menuItems: [
      { id: 'm-1', dishId: 'd-1', priceAmount: 120, priceCurrency: 'UAH' },
      { id: 'm-2', dishId: 'd-2', priceAmount: 80, priceCurrency: 'UAH' },
    ],
  };
}

const ERROR: ApiError = {
  kind: 'validation',
  status: 400,
  code: 'Validation',
  message: 'bad',
  messageKey: 'errors.validation',
};

describe('ProtectionFlagsComponent', () => {
  let details$: Subject<RestaurantDetailsDto>;
  let itemCalls: { id: string; value: boolean }[];
  let restaurantCalls: { id: string; value: boolean }[];
  let itemResult: () => Observable<void>;
  let restaurantResult: () => Observable<void>;

  beforeEach(async () => {
    details$ = new Subject<RestaurantDetailsDto>();
    itemCalls = [];
    restaurantCalls = [];
    itemResult = () => of(undefined);
    restaurantResult = () => of(undefined);

    await TestBed.configureTestingModule({
      imports: [ProtectionFlagsComponent],
      providers: [
        provideNoopAnimations(),
        { provide: GET_RESTAURANT_DETAILS, useValue: { execute: () => details$.asObservable() } },
        {
          provide: SET_MENU_ITEM_DO_NOT_PARSE,
          useValue: {
            execute: (id: string, value: boolean) => {
              itemCalls.push({ id, value });
              return itemResult();
            },
          },
        },
        {
          provide: SET_RESTAURANT_DO_NOT_UPDATE,
          useValue: {
            execute: (id: string, value: boolean) => {
              restaurantCalls.push({ id, value });
              return restaurantResult();
            },
          },
        },
      ],
    }).compileComponents();
  });

  function render(): ComponentFixture<ProtectionFlagsComponent> {
    const fixture = TestBed.createComponent(ProtectionFlagsComponent);
    fixture.detectChanges();
    return fixture;
  }

  function el(fixture: ComponentFixture<ProtectionFlagsComponent>, testid: string): Element | null {
    return fixture.nativeElement.querySelector(`[data-testid="${testid}"]`);
  }

  function load(fixture: ComponentFixture<ProtectionFlagsComponent>): void {
    fixture.componentInstance.loadRestaurant('r-1');
    fixture.detectChanges();
    details$.next(details());
    details$.complete();
    fixture.detectChanges();
  }

  it('renders the restaurant-level and per-item toggles after a load', () => {
    const fixture = render();
    load(fixture);
    expect(el(fixture, 'restaurant-do-not-update')).not.toBeNull();
    expect(el(fixture, 'item-do-not-parse-m-1')).not.toBeNull();
    expect(el(fixture, 'item-do-not-parse-m-2')).not.toBeNull();
  });

  it('sets the restaurant do-not-update flag with the {value} body (204 success)', () => {
    const fixture = render();
    load(fixture);
    fixture.componentInstance.setRestaurantDoNotUpdate('r-1', true);
    fixture.detectChanges();
    expect(restaurantCalls).toEqual([{ id: 'r-1', value: true }]);
  });

  it('sets a menu item do-not-parse flag with the {value} body (204 success)', () => {
    const fixture = render();
    load(fixture);
    fixture.componentInstance.setItemDoNotParse('m-1', true);
    fixture.detectChanges();
    expect(itemCalls).toEqual([{ id: 'm-1', value: true }]);
  });

  it('surfaces the typed error and reverts the toggle when a restaurant flag write fails', () => {
    const fixture = render();
    load(fixture);
    restaurantResult = () => throwError(() => ERROR);
    fixture.componentInstance.setRestaurantDoNotUpdate('r-1', true);
    fixture.detectChanges();
    expect(el(fixture, 'restaurant-flag-error')).not.toBeNull();
    // The flag reverts to its prior (off) value so the UI never claims an unsaved state.
    expect(fixture.componentInstance.restaurantFlag().value).toBe(false);
  });

  it('surfaces the typed error per item when an item flag write fails', () => {
    const fixture = render();
    load(fixture);
    itemResult = () => throwError(() => ERROR);
    fixture.componentInstance.setItemDoNotParse('m-1', true);
    fixture.detectChanges();
    expect(el(fixture, 'item-flag-error-m-1')).not.toBeNull();
    expect(fixture.componentInstance.itemFlag('m-1').value).toBe(false);
    // The other item is unaffected.
    expect(fixture.componentInstance.itemFlag('m-2').error).toBeUndefined();
  });

  it('renders the typed error state on a 404 and lists no toggles', () => {
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
    expect(el(fixture, 'restaurant-do-not-update')).toBeNull();
  });
});
