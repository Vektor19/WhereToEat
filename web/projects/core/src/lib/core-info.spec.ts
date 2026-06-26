import { CORE_LIBRARY_NAME } from './core-info';

describe('core public API', () => {
  it('exposes the core library marker through the public-api barrel', () => {
    expect(CORE_LIBRARY_NAME).toBe('core');
  });
});
