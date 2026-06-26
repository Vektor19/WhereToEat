import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { AppShellComponent } from './app-shell.component';

describe('AppShellComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AppShellComponent],
      providers: [provideRouter([]), provideNoopAnimations()],
    }).compileComponents();
  });

  function render(): HTMLElement {
    const fixture = TestBed.createComponent(AppShellComponent);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('renders the responsive shell landmarks (header / main / footer)', () => {
    const el = render();
    expect(el.querySelector('mat-toolbar')).not.toBeNull();
    expect(el.querySelector('main#dp-main-content')).not.toBeNull();
    expect(el.querySelector('footer[role="contentinfo"]')).not.toBeNull();
  });

  it('exposes a language-switch slot for Step 17', () => {
    const el = render();
    expect(el.querySelector('[data-testid="lang-switch-slot"]')).not.toBeNull();
  });

  it('renders exactly one data-accuracy notice (single discreet ⓘ carrier)', () => {
    const el = render();
    const notices = el.querySelectorAll('[data-testid="data-accuracy-notice"]');
    expect(notices.length).toBe(1);
  });

  it('contains no "updated X days ago" copy anywhere in the shell', () => {
    const el = render();
    const text = (el.textContent ?? '').toLowerCase();
    expect(text).not.toContain('оновлено');
    expect(text).not.toContain('updated');
    expect(text).not.toMatch(/days ago/);
  });

  it('provides a keyboard skip link to the main content', () => {
    const el = render();
    const skip = el.querySelector('a.dp-skip-link') as HTMLAnchorElement | null;
    expect(skip).not.toBeNull();
    expect(skip?.getAttribute('href')).toBe('#dp-main-content');
  });
});
