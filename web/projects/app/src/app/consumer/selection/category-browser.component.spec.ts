import { TestBed, type ComponentFixture } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import type { ApiError, CategoryDto, DishDto } from 'core';

import { CategoryBrowserComponent } from './category-browser.component';

const CATEGORIES: readonly CategoryDto[] = [
  { id: 'c-1', name: 'Перші страви' },
  { id: 'c-2', name: 'Фаст-фуд' },
];

const DISHES: readonly DishDto[] = [
  { id: 'd-1', categoryId: 'c-1', canonicalName: 'Борщ' },
  { id: 'd-2', categoryId: 'c-1', canonicalName: 'Суп' },
];

function serverError(): ApiError {
  return {
    kind: 'server',
    status: 500,
    code: 'Http.ServerError',
    message: 'boom',
    messageKey: 'errors.server',
  };
}

describe('CategoryBrowserComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CategoryBrowserComponent],
      providers: [provideNoopAnimations()],
    }).compileComponents();
  });

  function createWith(inputs: Record<string, unknown>): ComponentFixture<CategoryBrowserComponent> {
    const fixture = TestBed.createComponent(CategoryBrowserComponent);
    for (const [name, value] of Object.entries(inputs)) {
      fixture.componentRef.setInput(name, value);
    }
    fixture.detectChanges();
    return fixture;
  }

  it('lists categories at the top level and emits openCategoryRequest on drill-in', () => {
    const fixture = createWith({ categories: CATEGORIES });
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('[data-testid="category-list"]')).not.toBeNull();
    expect(el.querySelectorAll('[data-testid="category-list"] li').length).toBe(2);

    let opened: CategoryDto | undefined;
    fixture.componentInstance.openCategoryRequest.subscribe((c) => (opened = c));
    (el.querySelector('[data-testid="category-c-1"]') as HTMLButtonElement).click();
    expect(opened).toEqual(CATEGORIES[0]);
  });

  it('emits addCategory when adding a whole category from the top level', () => {
    const fixture = createWith({ categories: CATEGORIES });
    let added: CategoryDto | undefined;
    fixture.componentInstance.addCategory.subscribe((c) => (added = c));
    (
      fixture.nativeElement.querySelector('[data-testid="add-category-c-2"]') as HTMLButtonElement
    ).click();
    expect(added).toEqual(CATEGORIES[1]);
  });

  it('lists the opened category dishes and emits addDish for a specific dish', () => {
    const fixture = createWith({ openCategory: CATEGORIES[0], dishes: DISHES });
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('[data-testid="dish-list"]')).not.toBeNull();
    expect(el.querySelectorAll('[data-testid="dish-list"] li').length).toBe(2);

    let added: DishDto | undefined;
    fixture.componentInstance.addDish.subscribe((d) => (added = d));
    (el.querySelector('[data-testid="dish-d-1"]') as HTMLButtonElement).click();
    expect(added).toEqual(DISHES[0]);
  });

  it('emits closeCategory from the drill-in back button', () => {
    const fixture = createWith({ openCategory: CATEGORIES[0], dishes: DISHES });
    let closed = false;
    fixture.componentInstance.closeCategory.subscribe(() => (closed = true));
    (
      fixture.nativeElement.querySelector('[data-testid="browse-back"]') as HTMLButtonElement
    ).click();
    expect(closed).toBe(true);
  });

  it('disables an already-selected category and dish (shown as added)', () => {
    const el = createWith({
      openCategory: CATEGORIES[0],
      dishes: DISHES,
      selectedCategoryIds: ['c-1'],
      selectedDishIds: ['d-1'],
    }).nativeElement as HTMLElement;
    const addOpenCategory = el.querySelector(
      '[data-testid="browse-add-open-category"]',
    ) as HTMLButtonElement;
    expect(addOpenCategory.disabled).toBe(true);
    expect((el.querySelector('[data-testid="dish-d-1"]') as HTMLButtonElement).disabled).toBe(true);
    expect((el.querySelector('[data-testid="dish-d-2"]') as HTMLButtonElement).disabled).toBe(
      false,
    );
  });

  it('renders loading / empty / error via the shared primitives', () => {
    expect(
      (createWith({ categoriesLoading: true }).nativeElement as HTMLElement).querySelector(
        'app-loading',
      ),
    ).not.toBeNull();
    expect(
      (createWith({ categoriesEmpty: true }).nativeElement as HTMLElement).querySelector(
        'app-error-state',
      ),
    ).not.toBeNull();
    expect(
      (createWith({ categoriesError: serverError() }).nativeElement as HTMLElement).querySelector(
        'app-error-state',
      ),
    ).not.toBeNull();
  });

  it('emits reloadCategories from the categories error retry', () => {
    const fixture = createWith({ categoriesError: serverError() });
    let reloaded = false;
    fixture.componentInstance.reloadCategories.subscribe(() => (reloaded = true));
    (
      fixture.nativeElement.querySelector('[data-testid="error-retry"]') as HTMLButtonElement
    ).click();
    expect(reloaded).toBe(true);
  });
});
