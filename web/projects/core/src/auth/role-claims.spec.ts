import { mergeRoles, parseRolesFromClaims } from './role-claims';

describe('parseRolesFromClaims', () => {
  it('maps the Keycloak realm-role placement (realm_access.roles)', () => {
    const roles = parseRolesFromClaims({ realm_access: { roles: ['admin', 'offline_access'] } });
    expect([...roles]).toEqual(['Admin']);
  });

  it('maps the Keycloak client-role placement (resource_access.{client}.roles)', () => {
    const roles = parseRolesFromClaims({
      resource_access: {
        'wheretoeat-spa': { roles: ['user'] },
        account: { roles: ['manage-account'] },
      },
    });
    expect([...roles]).toEqual(['User']);
  });

  it('maps a flattened single-string role claim', () => {
    expect([...parseRolesFromClaims({ role: 'admin' })]).toEqual(['Admin']);
  });

  it('maps a flattened roles array claim', () => {
    expect([...parseRolesFromClaims({ roles: ['user', 'admin'] })].sort()).toEqual([
      'Admin',
      'User',
    ]);
  });

  it('maps the admin role regardless of claim shape (realm-role vs flat)', () => {
    expect(parseRolesFromClaims({ realm_access: { roles: ['Admin'] } })).toContain('Admin');
    expect(parseRolesFromClaims({ role: 'ADMIN' })).toContain('Admin');
    expect(parseRolesFromClaims({ roles: ['admin'] })).toContain('Admin');
  });

  it('is case-insensitive and trims whitespace', () => {
    expect([...parseRolesFromClaims({ role: '  Admin  ' })]).toEqual(['Admin']);
  });

  it('ignores unknown role strings rather than failing', () => {
    expect([...parseRolesFromClaims({ realm_access: { roles: ['superuser', 'guest'] } })]).toEqual(
      [],
    );
  });

  it('de-duplicates a role present in several placements', () => {
    const roles = parseRolesFromClaims({
      realm_access: { roles: ['admin'] },
      role: 'admin',
      roles: ['admin'],
    });
    expect([...roles]).toEqual(['Admin']);
  });

  it('returns an empty array for nullish or non-object claims', () => {
    expect([...parseRolesFromClaims(null)]).toEqual([]);
    expect([...parseRolesFromClaims(undefined)]).toEqual([]);
  });

  it('tolerates malformed access containers', () => {
    expect([...parseRolesFromClaims({ realm_access: 'nope' })]).toEqual([]);
    expect([...parseRolesFromClaims({ resource_access: 42 })]).toEqual([]);
  });
});

describe('mergeRoles', () => {
  it('unions token and identity roles without duplicates', () => {
    const merged = mergeRoles(['Admin'], ['User', 'Admin']);
    expect([...merged].sort()).toEqual(['Admin', 'User']);
  });

  it('returns token roles when identity is empty', () => {
    expect([...mergeRoles(['User'], [])]).toEqual(['User']);
  });
});
