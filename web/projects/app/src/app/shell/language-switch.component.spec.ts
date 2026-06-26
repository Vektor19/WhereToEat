import { signal } from '@angular/core';
import { TestBed, type ComponentFixture } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { LocaleService } from 'core';

import { LanguageSwitchComponent } from './language-switch.component';

/**
 * A {@link LocaleService} double: records the requested locale on `use()` and lets the test drive the
 * active-locale signal, so the switch component is tested in isolation from ngx-translate / DOM.
 */
class LocaleServiceStub {
  readonly used: string[] = [];
  readonly activeLocale = signal<string>('uk');
  readonly locales = [
    { code: 'uk', label: 'Українська' },
    { code: 'en', label: 'English' },
  ] as const;
  use(code: string): void {
    this.used.push(code);
    this.activeLocale.set(code);
  }
}

describe('LanguageSwitchComponent', () => {
  let locale: LocaleServiceStub;

  beforeEach(async () => {
    locale = new LocaleServiceStub();
    await TestBed.configureTestingModule({
      imports: [LanguageSwitchComponent],
      providers: [provideNoopAnimations(), { provide: LocaleService, useValue: locale }],
    }).compileComponents();
  });

  function render(): ComponentFixture<LanguageSwitchComponent> {
    const fixture = TestBed.createComponent(LanguageSwitchComponent);
    fixture.detectChanges();
    return fixture;
  }

  it('renders an accessible, labelled menu trigger showing the active locale code', () => {
    const fixture = render();
    const trigger = fixture.nativeElement.querySelector(
      '[data-testid="lang-switch"]',
    ) as HTMLElement;
    expect(trigger).not.toBeNull();
    expect(trigger.getAttribute('aria-haspopup')).toBe('menu');
    // The visible code reflects the active locale (uppercased).
    expect((trigger.textContent ?? '').toUpperCase()).toContain('UK');
  });

  it('switches the locale through LocaleService.use when an option is selected', () => {
    const fixture = render();
    // The menu items render lazily; drive the component method the menu item binds to.
    fixture.componentInstance.select('en');
    expect(locale.used).toEqual(['en']);
  });

  it('reflects the new active locale code after a switch', () => {
    const fixture = render();
    fixture.componentInstance.select('en');
    fixture.detectChanges();
    const trigger = fixture.nativeElement.querySelector(
      '[data-testid="lang-switch"]',
    ) as HTMLElement;
    expect((trigger.textContent ?? '').toUpperCase()).toContain('EN');
  });

  it('offers every supported locale by its autonym label', () => {
    const fixture = render();
    expect(fixture.componentInstance.locales.map((l) => l.label)).toEqual([
      'Українська',
      'English',
    ]);
  });
});
