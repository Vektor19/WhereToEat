import { decodeJwtClaims } from './jwt';

/** Build a compact JWT with the given payload (header/signature are placeholders — unverified). */
function makeJwt(payload: Record<string, unknown>): string {
  const b64url = (obj: unknown): string => {
    const utf8 = new TextEncoder().encode(JSON.stringify(obj));
    const binary = Array.from(utf8, (byte) => String.fromCharCode(byte)).join('');
    return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
  };
  return `${b64url({ alg: 'RS256', typ: 'JWT' })}.${b64url(payload)}.signature`;
}

describe('decodeJwtClaims', () => {
  it('decodes the payload of a well-formed JWT', () => {
    const token = makeJwt({ sub: 'user-123', realm_access: { roles: ['admin'] } });
    const claims = decodeJwtClaims(token);
    expect(claims?.['sub']).toBe('user-123');
    expect(claims?.['realm_access']).toEqual({ roles: ['admin'] });
  });

  it('decodes a payload containing non-ASCII (UTF-8) characters', () => {
    const token = makeJwt({ name: 'Поїсти' });
    expect(decodeJwtClaims(token)?.['name']).toBe('Поїсти');
  });

  it('returns null for an absent token', () => {
    expect(decodeJwtClaims(null)).toBeNull();
    expect(decodeJwtClaims(undefined)).toBeNull();
    expect(decodeJwtClaims('')).toBeNull();
  });

  it('returns null when the token is not a three-segment JWT', () => {
    expect(decodeJwtClaims('not-a-jwt')).toBeNull();
    expect(decodeJwtClaims('only.two')).toBeNull();
  });

  it('returns null when the payload segment is not valid JSON', () => {
    const bad = `${btoa('header')}.${btoa('not json')}.sig`;
    expect(decodeJwtClaims(bad)).toBeNull();
  });
});
