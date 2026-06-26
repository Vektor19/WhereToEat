import { TestBed, type ComponentFixture } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';

import { RestaurantPickerComponent } from './restaurant-picker.component';

describe('RestaurantPickerComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [RestaurantPickerComponent],
      providers: [provideNoopAnimations()],
    }).compileComponents();
  });

  function render(): ComponentFixture<RestaurantPickerComponent> {
    const fixture = TestBed.createComponent(RestaurantPickerComponent);
    fixture.detectChanges();
    return fixture;
  }

  function el(
    fixture: ComponentFixture<RestaurantPickerComponent>,
    testid: string,
  ): Element | null {
    return fixture.nativeElement.querySelector(`[data-testid="${testid}"]`);
  }

  function type(fixture: ComponentFixture<RestaurantPickerComponent>, value: string): void {
    const input = el(fixture, 'restaurant-id-input') as HTMLInputElement;
    input.value = value;
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
  }

  it('emits the trimmed id on submit', () => {
    const fixture = render();
    let loaded: string | undefined;
    fixture.componentInstance.loadId.subscribe((id) => (loaded = id));
    type(fixture, '  r-1  ');
    (el(fixture, 'restaurant-picker') as HTMLFormElement).dispatchEvent(new Event('submit'));
    expect(loaded).toBe('r-1');
  });

  it('does not emit for a blank id', () => {
    const fixture = render();
    let emitted = false;
    fixture.componentInstance.loadId.subscribe(() => (emitted = true));
    type(fixture, '   ');
    (el(fixture, 'restaurant-picker') as HTMLFormElement).dispatchEvent(new Event('submit'));
    expect(emitted).toBe(false);
  });

  it('disables the load button while loading', () => {
    const fixture = render();
    type(fixture, 'r-1');
    fixture.componentRef.setInput('loading', true);
    fixture.detectChanges();
    expect((el(fixture, 'restaurant-load') as HTMLButtonElement).disabled).toBe(true);
  });
});
