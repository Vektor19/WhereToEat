import { signal, type Signal } from '@angular/core';
import { By } from '@angular/platform-browser';
import { TestBed, type ComponentFixture } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { Subject, of, throwError, type Observable } from 'rxjs';

import { MenuEditFormComponent } from './menu-edit-form.component';
import {
  CatalogStore,
  EDIT_MENU_ITEM,
  GET_RESTAURANT_DETAILS,
  type ApiError,
  type CategoryDto,
  type DishDto,
  type EditMenuItemBody,
  type RestaurantDetailsDto,
} from 'core';

import { MenuManagementComponent } from './menu-management.component';

/**
 * A minimal {@link CatalogStore} stub exposing only the surface the menu container reads: the loaded
 * categories, per-category dishes (read + load), the dish-name lookup, and `loadCategories`. The real
 * store pulls APP_CONFIG/HTTP, so it is stubbed to keep the component test focused.
 */
const CATEGORIES: readonly CategoryDto[] = [
  { id: 'cat-1', name: 'Перші страви' },
  { id: 'cat-2', name: 'Фаст-фуд' },
];
const DISHES: Record<string, readonly DishDto[]> = {
  'cat-1': [{ id: 'd-1', categoryId: 'cat-1', canonicalName: 'Борщ' }],
  'cat-2': [{ id: 'd-2', categoryId: 'cat-2', canonicalName: 'Піца Маргарита' }],
};

class StubCatalogStore {
  loadedCategories = signal<readonly CategoryDto[] | undefined>(CATEGORIES);
  loadCategoriesCalls = 0;
  loadDishesCalls: string[] = [];

  categories(): readonly CategoryDto[] | undefined {
    return this.loadedCategories();
  }
  loadCategories(): void {
    this.loadCategoriesCalls += 1;
  }
  loadDishes(categoryId: string): void {
    this.loadDishesCalls.push(categoryId);
  }
  dishes(categoryId: string): Signal<readonly DishDto[] | undefined> {
    return signal(DISHES[categoryId]);
  }
  dishesLoading(): Signal<boolean> {
    return signal(false);
  }
  dishNameById(dishId: string): Signal<string | undefined> {
    return signal(
      Object.values(DISHES)
        .flat()
        .find((d) => d.id === dishId)?.canonicalName,
    );
  }
}

function details(over: Partial<RestaurantDetailsDto> = {}): RestaurantDetailsDto {
  return {
    id: 'r-1',
    name: 'Тест',
    addressLine: 'вул. Хрещатик, 1',
    addressCity: 'Київ',
    contactLinks: [],
    menuItems: [
      { id: 'm-1', dishId: 'd-1', priceAmount: 120, priceCurrency: 'UAH', weight: '300 г' },
    ],
    ...over,
  };
}

const NOT_FOUND: ApiError = {
  kind: 'not-found',
  status: 404,
  code: 'NotFound',
  message: 'missing',
  messageKey: 'errors.notFound',
};

describe('MenuManagementComponent', () => {
  let details$: Subject<RestaurantDetailsDto>;
  let editCalls: { id: string; body: EditMenuItemBody }[];
  let editResult: () => Observable<void>;

  beforeEach(async () => {
    details$ = new Subject<RestaurantDetailsDto>();
    editCalls = [];
    editResult = () => of(undefined);

    await TestBed.configureTestingModule({
      imports: [MenuManagementComponent],
      providers: [
        provideNoopAnimations(),
        { provide: CatalogStore, useClass: StubCatalogStore },
        { provide: GET_RESTAURANT_DETAILS, useValue: { execute: () => details$.asObservable() } },
        {
          provide: EDIT_MENU_ITEM,
          useValue: {
            execute: (id: string, body: EditMenuItemBody) => {
              editCalls.push({ id, body });
              return editResult();
            },
          },
        },
      ],
    }).compileComponents();
  });

  function render(): ComponentFixture<MenuManagementComponent> {
    const fixture = TestBed.createComponent(MenuManagementComponent);
    fixture.detectChanges();
    return fixture;
  }

  function el(fixture: ComponentFixture<MenuManagementComponent>, testid: string): Element | null {
    return fixture.nativeElement.querySelector(`[data-testid="${testid}"]`);
  }

  function load(fixture: ComponentFixture<MenuManagementComponent>): void {
    fixture.componentInstance.loadRestaurant('r-1');
    fixture.detectChanges();
    details$.next(details());
    details$.complete();
    fixture.detectChanges();
  }

  it('lists the restaurant menu items after a load', () => {
    const fixture = render();
    load(fixture);
    expect(el(fixture, 'menu-item-m-1')).not.toBeNull();
    // The dish name resolves from the (stub) taxonomy.
    expect(el(fixture, 'menu-item-m-1')?.textContent).toContain('Борщ');
  });

  it('renders the typed error state on a 404 and does not list items', () => {
    const fixture = render();
    fixture.componentInstance.loadRestaurant('r-1');
    fixture.detectChanges();
    details$.error(NOT_FOUND);
    fixture.detectChanges();
    expect(el(fixture, 'error-state')).not.toBeNull();
    expect(el(fixture, 'menu-item-m-1')).toBeNull();
  });

  it('opens the edit form for the selected item seeded from its values', () => {
    const fixture = render();
    load(fixture);
    (el(fixture, 'menu-item-m-1') as HTMLElement).click();
    fixture.detectChanges();
    expect(el(fixture, 'menu-edit-form')).not.toBeNull();
    expect((el(fixture, 'menu-price-input') as HTMLInputElement).value).toBe('120');
    expect((el(fixture, 'menu-currency-input') as HTMLInputElement).value).toBe('UAH');
  });

  it('saves the edit to PUT /admin/menu-items/{id} with the exact body (204 success)', () => {
    const fixture = render();
    load(fixture);
    (el(fixture, 'menu-item-m-1') as HTMLElement).click();
    fixture.detectChanges();

    // Edit price + weight (the dish/currency keep their seeded values).
    const priceInput = el(fixture, 'menu-price-input') as HTMLInputElement;
    priceInput.value = '150';
    priceInput.dispatchEvent(new Event('input'));
    const weightInput = el(fixture, 'menu-weight-input') as HTMLInputElement;
    weightInput.value = '350 г';
    weightInput.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    (el(fixture, 'menu-edit-form') as HTMLFormElement).dispatchEvent(new Event('submit'));
    fixture.detectChanges();

    expect(editCalls.length).toBe(1);
    expect(editCalls[0].id).toBe('m-1');
    expect(editCalls[0].body).toEqual({
      dishId: 'd-1',
      priceAmount: 150,
      priceCurrency: 'UAH',
      weight: '350 г',
    });
    expect(el(fixture, 'save-success')).not.toBeNull();
  });

  it('re-categorises by changing the dish, sending the new dishId in the body', () => {
    const fixture = render();
    load(fixture);
    (el(fixture, 'menu-item-m-1') as HTMLElement).click();
    fixture.detectChanges();

    // Change the dish to d-2 directly (the dish select is bound to the dishId form control).
    const editForm = fixture.debugElement.query(By.directive(MenuEditFormComponent))
      .componentInstance as MenuEditFormComponent;
    editForm.form.controls['dishId'].setValue('d-2');
    fixture.detectChanges();
    (el(fixture, 'menu-edit-form') as HTMLFormElement).dispatchEvent(new Event('submit'));
    fixture.detectChanges();

    expect(editCalls.length).toBe(1);
    expect(editCalls[0].body.dishId).toBe('d-2');
  });

  it('surfaces the typed error from a failed save (400/404)', () => {
    const fixture = render();
    load(fixture);
    (el(fixture, 'menu-item-m-1') as HTMLElement).click();
    fixture.detectChanges();
    editResult = () => throwError(() => NOT_FOUND);

    (el(fixture, 'menu-edit-form') as HTMLFormElement).dispatchEvent(new Event('submit'));
    fixture.detectChanges();

    expect(el(fixture, 'save-error')).not.toBeNull();
    expect(
      el(fixture, 'save-error')
        ?.querySelector('[data-message-key]')
        ?.getAttribute('data-message-key'),
    ).toBe('errors.notFound');
  });
});
