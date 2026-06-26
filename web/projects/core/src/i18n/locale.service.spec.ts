import { DOCUMENT } from '@angular/common';
import { TestBed } from '@angular/core/testing';
import { TranslateService } from '@ngx-translate/core';

import { DEFAULT_LOCALE, LOCALE_STORAGE_KEY } from './locale.config';
import { LocaleService } from './locale.service';

/**
 * A minimal {@link TranslateService} double recording the language activations and the fallback, so
 * the switch logic is asserted without pulling the real ngx-translate runtime (and its JIT-compiler
 * dependency) into the `core` unit environment. Only the members {@link LocaleService} touches exist.
 */
class TranslateStub {
  used: string[] = [];
  fallback: string | null = null;
  setFallbackLang(code: string): void {
    this.fallback = code;
  }
  use(code: string): void {
    this.used.push(code);
  }
}

/** An in-memory localStorage double so persistence is asserted without a real Storage. */
class StorageStub {
  private readonly store = new Map<string, string>();
  getItem(key: string): string | null {
    return this.store.get(key) ?? null;
  }
  setItem(key: string, value: string): void {
    this.store.set(key, value);
  }
}

function configure(persisted?: string): {
  service: LocaleService;
  translate: TranslateStub;
  storage: StorageStub;
  documentEl: { documentElement: { lang: string } };
} {
  const translate = new TranslateStub();
  const storage = new StorageStub();
  if (persisted !== undefined) {
    storage.setItem(LOCALE_STORAGE_KEY, persisted);
  }
  const documentEl = {
    documentElement: { lang: '' },
    defaultView: { localStorage: storage },
  };

  TestBed.configureTestingModule({
    providers: [
      LocaleService,
      { provide: TranslateService, useValue: translate },
      { provide: DOCUMENT, useValue: documentEl },
    ],
  });

  return { service: TestBed.inject(LocaleService), translate, storage, documentEl };
}

describe('LocaleService', () => {
  it('exposes uk as the default active locale before any switch', () => {
    const { service } = configure();
    expect(service.activeLocale()).toBe(DEFAULT_LOCALE);
  });

  it('init registers the uk fallback and activates the default when nothing is persisted', () => {
    const { service, translate } = configure();
    service.init();
    expect(translate.fallback).toBe(DEFAULT_LOCALE);
    expect(translate.used).toContain(DEFAULT_LOCALE);
    expect(service.activeLocale()).toBe(DEFAULT_LOCALE);
  });

  it('init activates a previously persisted (supported) locale', () => {
    const { service, translate } = configure('en');
    service.init();
    expect(translate.used).toContain('en');
    expect(service.activeLocale()).toBe('en');
  });

  it('init ignores a persisted unsupported locale and falls back to uk', () => {
    const { service } = configure('fr');
    service.init();
    expect(service.activeLocale()).toBe(DEFAULT_LOCALE);
  });

  it('use() swaps the active catalog, sets document.lang, updates the signal, and persists', () => {
    const { service, translate, storage, documentEl } = configure();
    service.use('en');

    expect(translate.used).toContain('en');
    expect(documentEl.documentElement.lang).toBe('en');
    expect(service.activeLocale()).toBe('en');
    expect(storage.getItem(LOCALE_STORAGE_KEY)).toBe('en');
  });

  it('use() ignores an unsupported code (active locale unchanged, never blanks the UI)', () => {
    const { service, translate, documentEl } = configure();
    service.use('uk');
    documentEl.documentElement.lang = 'uk';
    service.use('fr');

    expect(service.activeLocale()).toBe('uk');
    expect(translate.used).not.toContain('fr');
    expect(documentEl.documentElement.lang).toBe('uk');
  });

  it('exposes the supported locales (autonym labels) for the switch', () => {
    const { service } = configure();
    expect(service.locales.map((l) => l.code)).toContain('uk');
    expect(service.locales.map((l) => l.code)).toContain('en');
  });
});
