import { Component, signal } from '@angular/core';
import { TestBed, type ComponentFixture } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';

import type {
  ConversionFunnelDto,
  DemandBreakdownDto,
  PricePositioningDto,
  RatingsDistributionDto,
} from 'core';

import { ConversionFunnelComponent } from './conversion-funnel.component';
import { DemandBreakdownComponent } from './demand-breakdown.component';
import { PricePositioningComponent } from './price-positioning.component';
import { RatingsDistributionComponent } from './ratings-distribution.component';

function qa(fixture: ComponentFixture<unknown>, testid: string): Element | null {
  return fixture.nativeElement.querySelector(`[data-testid="${testid}"]`);
}

describe('ConversionFunnelComponent', () => {
  @Component({
    imports: [ConversionFunnelComponent],
    template: `<app-analytics-funnel [funnel]="funnel()" />`,
  })
  class Host {
    readonly funnel = signal<ConversionFunnelDto>({
      restaurantId: 'r-1',
      impressions: 1000,
      cardOpens: 250,
      actions: 50,
      impressionToCardOpenRate: 0.25,
      cardOpenToActionRate: 0.2,
    });
  }

  it('renders the three stage counts and the two conversion rates', async () => {
    await TestBed.configureTestingModule({
      imports: [Host],
      providers: [provideNoopAnimations()],
    }).compileComponents();
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();

    expect(qa(fixture, 'funnel-impressions')?.textContent).toContain('1000');
    expect(qa(fixture, 'funnel-card-opens')?.textContent).toContain('250');
    expect(qa(fixture, 'funnel-actions')?.textContent).toContain('50');
    expect(qa(fixture, 'funnel-open-rate')?.textContent).toContain('25.0%');
    expect(qa(fixture, 'funnel-action-rate')?.textContent).toContain('20.0%');
  });
});

describe('DemandBreakdownComponent', () => {
  @Component({
    imports: [DemandBreakdownComponent],
    template: `<app-analytics-demand [breakdown]="breakdown()" />`,
  })
  class Host {
    readonly breakdown = signal<DemandBreakdownDto>({
      categories: [{ selectionId: 'cat-1', searchCount: 50 }],
      dishes: [],
    });
  }

  it('renders a category demand row and an empty-state for dishes', async () => {
    await TestBed.configureTestingModule({
      imports: [Host],
      providers: [provideNoopAnimations()],
    }).compileComponents();
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();

    expect(qa(fixture, 'demand-category-cat-1')?.textContent).toContain('50');
    expect(qa(fixture, 'demand-dishes-empty')).not.toBeNull();
  });
});

describe('PricePositioningComponent', () => {
  @Component({
    imports: [PricePositioningComponent],
    template: `<app-analytics-price-positioning [positioning]="positioning()" />`,
  })
  class Host {
    readonly positioning = signal<PricePositioningDto>({
      restaurantId: 'r-1',
      dishes: [
        {
          dishId: 'cheap',
          venuePrice: 150,
          medianPrice: 170,
          deltaFromMedian: -20,
          currency: 'UAH',
        },
        {
          dishId: 'no-median',
          venuePrice: 90,
          medianPrice: null,
          deltaFromMedian: null,
          currency: 'UAH',
        },
      ],
    });
  }

  it('formats prices by currency and marks cheaper-than-market and missing-median rows', async () => {
    await TestBed.configureTestingModule({
      imports: [Host],
      providers: [provideNoopAnimations()],
    }).compileComponents();
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();

    const cheap = qa(fixture, 'price-row-cheap');
    expect(cheap).not.toBeNull();
    // Currency-formatted (UAH symbol/grouping comes from Intl, never a hardcoded symbol).
    expect(cheap?.textContent).toContain('150');
    // Cheaper-than-market marker carries the accessible label (text, not colour alone).
    expect(cheap?.textContent).toContain('Дешевше за ринок');

    const noMedian = qa(fixture, 'price-row-no-median');
    expect(noMedian?.textContent).toContain('Медіана недоступна');
    expect(noMedian?.textContent).toContain('—');
  });
});

describe('RatingsDistributionComponent', () => {
  @Component({
    imports: [RatingsDistributionComponent],
    template: `<app-analytics-ratings [distribution]="distribution()" />`,
  })
  class Host {
    readonly distribution = signal<RatingsDistributionDto>({
      restaurantId: 'r-1',
      ratingCount: 0,
      scoreSum: 0,
      averageScore: 0,
    });
  }

  it('renders the empty-state when there are no ratings yet', async () => {
    await TestBed.configureTestingModule({
      imports: [Host],
      providers: [provideNoopAnimations()],
    }).compileComponents();
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();

    expect(qa(fixture, 'ratings-empty')).not.toBeNull();
    expect(qa(fixture, 'ratings-average')).toBeNull();
  });
});
