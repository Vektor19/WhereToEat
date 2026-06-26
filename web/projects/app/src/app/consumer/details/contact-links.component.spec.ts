import { TestBed, type ComponentFixture } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';

import { ContactLinksComponent } from './contact-links.component';
import type { ContactLinkViewModel } from './details.view-model';

function link(over: Partial<ContactLinkViewModel> = {}): ContactLinkViewModel {
  return {
    kind: 'site',
    url: 'https://example.com',
    text: 'example.com',
    isPhone: false,
    ...over,
  };
}

describe('ContactLinksComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ContactLinksComponent],
      providers: [provideNoopAnimations()],
    }).compileComponents();
  });

  function render(links: readonly ContactLinkViewModel[]): ComponentFixture<ContactLinksComponent> {
    const fixture = TestBed.createComponent(ContactLinksComponent);
    fixture.componentRef.setInput('links', links);
    fixture.detectChanges();
    return fixture;
  }

  function anchors(fixture: ComponentFixture<ContactLinksComponent>): HTMLAnchorElement[] {
    return Array.from(
      fixture.nativeElement.querySelectorAll(
        '[data-testid="contact-link"]',
      ) as NodeListOf<HTMLAnchorElement>,
    );
  }

  it('renders the contact links for a venue (always free — §5.8)', () => {
    const fixture = render([
      link({ text: 'Сайт' }),
      link({ kind: 'instagram', text: 'Instagram' }),
    ]);
    expect(anchors(fixture).length).toBe(2);
  });

  it('renders web links as safe external links (target + rel)', () => {
    const fixture = render([link({ url: 'https://venue.example' })]);
    const [a] = anchors(fixture);
    expect(a.getAttribute('href')).toBe('https://venue.example');
    expect(a.getAttribute('target')).toBe('_blank');
    expect(a.getAttribute('rel')).toBe('noopener noreferrer');
  });

  it('renders a phone link in place (no new tab, tel: href)', () => {
    const fixture = render([
      link({ kind: 'phone', url: 'tel:+380441234567', text: '+380441234567', isPhone: true }),
    ]);
    const [a] = anchors(fixture);
    expect(a.getAttribute('href')).toBe('tel:+380441234567');
    // A phone link must not open a new tab.
    expect(a.getAttribute('target')).toBeNull();
  });

  it('shows the discreet empty note (never a paywall) when there are no links', () => {
    const fixture = render([]);
    expect(anchors(fixture).length).toBe(0);
    expect(
      fixture.nativeElement.querySelector('[data-testid="contact-links-empty"]'),
    ).not.toBeNull();
  });

  it('emits linkClick on a contact-link click (for the action analytics event)', () => {
    const fixture = render([link({ text: 'Сайт' })]);
    let clicked: ContactLinkViewModel | undefined;
    fixture.componentInstance.linkClick.subscribe((l) => (clicked = l));
    anchors(fixture)[0].click();
    expect(clicked?.text).toBe('Сайт');
  });

  it('is a labelled navigation region (a11y)', () => {
    const fixture = render([link()]);
    const nav = fixture.nativeElement.querySelector('[data-testid="contact-links"]');
    expect(nav?.tagName.toLowerCase()).toBe('nav');
    expect(nav?.getAttribute('aria-label')?.length).toBeGreaterThan(0);
  });
});
