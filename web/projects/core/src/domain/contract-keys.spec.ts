import {
  FILTER_KEYS,
  MATCH_MODES,
  SORT_MODES,
  type FilterKey,
  type MatchMode,
  type SortMode,
} from './contract-keys';

/**
 * Pins the contract key unions to the exact strings the backend engine accepts. If a value drifts
 * from the documented contract (CLAUDE.md §6 / Step 4), the request builder and query controls
 * would emit a key the engine rejects — these tests fail first.
 */
describe('contract keys', () => {
  it('MatchMode equals exactly the documented match keys', () => {
    expect([...MATCH_MODES].sort()).toEqual(['and', 'or']);
  });

  it('SortMode equals exactly the documented sort keys', () => {
    expect([...SORT_MODES].sort()).toEqual([
      'best',
      'distance',
      'price',
      'price-quality',
      'rating',
    ]);
  });

  it('FilterKey equals exactly the documented filter keys', () => {
    expect([...FILTER_KEYS].sort()).toEqual(['price', 'rating']);
  });

  it('the runtime lists are typed by their unions (compile-time guard)', () => {
    // These assignments would not compile if a list member fell outside its union.
    const match: readonly MatchMode[] = MATCH_MODES;
    const sort: readonly SortMode[] = SORT_MODES;
    const filter: readonly FilterKey[] = FILTER_KEYS;

    expect(match.length).toBe(2);
    expect(sort.length).toBe(5);
    expect(filter.length).toBe(2);
  });
});
