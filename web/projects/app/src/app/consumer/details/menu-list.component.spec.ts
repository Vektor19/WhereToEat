import { TestBed, type ComponentFixture } from '@angular/core/testing';

import { MenuListComponent } from './menu-list.component';
import type { MenuItemViewModel } from './details.view-model';

function item(over: Partial<MenuItemViewModel> = {}): MenuItemViewModel {
  return {
    id: 'm-1',
    dishName: 'Борщ',
    price: '120,00 ₴',
    weight: '350 г',
    photo: { isReal: false, src: 'generic-dish.svg', alt: 'Борщ' },
    ...over,
  };
}

describe('MenuListComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [MenuListComponent] }).compileComponents();
  });

  function render(items: readonly MenuItemViewModel[]): ComponentFixture<MenuListComponent> {
    const fixture = TestBed.createComponent(MenuListComponent);
    fixture.componentRef.setInput('items', items);
    fixture.detectChanges();
    return fixture;
  }

  function text(fixture: ComponentFixture<MenuListComponent>, testid: string): string {
    return (
      fixture.nativeElement.querySelector(`[data-testid="${testid}"]`)?.textContent ?? ''
    ).trim();
  }

  it('renders the menu items in order with the pre-formatted price and weight', () => {
    const fixture = render([
      item({ id: 'a', dishName: 'Борщ', price: '120,00 ₴', weight: '350 г' }),
      item({ id: 'b', dishName: 'Піца', price: '180,00 ₴', weight: '500 г' }),
    ]);
    expect(text(fixture, 'menu-item-name')).toBe('Борщ');
    expect(text(fixture, 'menu-item-price')).toBe('120,00 ₴');
    expect(text(fixture, 'menu-item-weight')).toBe('350 г');
    expect(fixture.nativeElement.querySelectorAll('[data-testid="menu-item-name"]').length).toBe(2);
  });

  it('omits the weight row when an item carries no weight', () => {
    const fixture = render([item({ weight: null })]);
    expect(fixture.nativeElement.querySelector('[data-testid="menu-item-weight"]')).toBeNull();
    // The price is still rendered.
    expect(fixture.nativeElement.querySelector('[data-testid="menu-item-price"]')).not.toBeNull();
  });

  it('renders generic photos by default (invariant #8)', () => {
    const fixture = render([item()]);
    expect(fixture.nativeElement.querySelector('[data-testid="generic-photo"]')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('[data-testid="real-photo"]')).toBeNull();
  });

  it('uses the real photo only when permitted', () => {
    const fixture = render([
      item({ photo: { isReal: true, src: 'real.jpg', alt: 'Справжнє фото' } }),
    ]);
    expect(fixture.nativeElement.querySelector('[data-testid="real-photo"]')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('[data-testid="generic-photo"]')).toBeNull();
  });

  it('renders no per-item price disclaimer or approximate marker (invariant #12 / §10)', () => {
    const fixture = render([item({ price: '120,00 ₴' })]);
    const html = fixture.nativeElement.textContent ?? '';
    expect(html).not.toContain('≈');
    expect(html).not.toContain('орієнтов');
    expect(html.toLowerCase()).not.toContain('оновлено');
  });

  it('shows the empty note when the menu is empty', () => {
    const fixture = render([]);
    expect(fixture.nativeElement.querySelector('[data-testid="menu-list-empty"]')).not.toBeNull();
  });
});
