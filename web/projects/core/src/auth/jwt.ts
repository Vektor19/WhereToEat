/**
 * Minimal, dependency-free JWT payload decoder.
 *
 * Keycloak carries realm/client roles in the **access token**, not the id token, so the auth seam
 * needs to read the access-token payload to derive roles. This decodes the payload segment ONLY —
 * it does **not** verify the signature (verification is the backend resource server's job; the
 * frontend uses the claims purely to drive client-side role gating and the token itself is still
 * validated server-side on every authenticated call). Returns `null` for any malformed input rather
 * than throwing, so a corrupt or absent token degrades to "no roles" instead of breaking the app.
 *
 * Kept framework-light (no Angular DI, no Node `Buffer`) so it runs in the browser and mirrors
 * cleanly into the React Native rewrite.
 */
import type { TokenClaims } from './role-claims';

/** Base64url → UTF-8 string, using the browser-native `atob` + `TextDecoder`. */
function decodeBase64Url(segment: string): string | null {
  try {
    const base64 = segment.replace(/-/g, '+').replace(/_/g, '/');
    const padded = base64.padEnd(base64.length + ((4 - (base64.length % 4)) % 4), '=');
    const binary = atob(padded);
    const bytes = Uint8Array.from(binary, (char) => char.charCodeAt(0));
    return new TextDecoder('utf-8').decode(bytes);
  } catch {
    return null;
  }
}

/**
 * Decode the claims (payload) of a compact JWS/JWT. Returns the parsed claims object, or `null` if
 * the token is absent, not a three-segment JWT, or has an unparseable payload.
 */
export function decodeJwtClaims(token: string | null | undefined): TokenClaims | null {
  if (!token) {
    return null;
  }

  const parts = token.split('.');
  if (parts.length !== 3) {
    return null;
  }

  const json = decodeBase64Url(parts[1]);
  if (json === null) {
    return null;
  }

  try {
    const parsed: unknown = JSON.parse(json);
    return typeof parsed === 'object' && parsed !== null ? (parsed as TokenClaims) : null;
  } catch {
    return null;
  }
}
