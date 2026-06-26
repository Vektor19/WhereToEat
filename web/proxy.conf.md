# `proxy.conf.json` — dev-only local API gateway

The SPA uses **relative `/api/...` paths in every environment**. This proxy stands in for the
deployed reverse proxy / API gateway in development, mapping the two logical-host prefixes behind
one origin to the two backend ports.

- CORS is handled here (`changeOrigin: true`), not by per-host backend config.
- Backend hosts serve their routes at the **root**, so each prefix is stripped via `pathRewrite`.
- Ports match `deploy/docker-compose.yml`: **PublicApi 8080**, **AdminApi 8081** (override with
  `PUBLIC_API_PORT` / `ADMIN_API_PORT` when running the hosts on other ports).

## Why the array form

Angular's `loadProxyConfiguration` passes the parsed JSON verbatim to Vite's `server.proxy`,
which treats **every top-level key as a proxy context string**. With the object form, helper keys
like `$schema` or `//` (comment) would register as bogus proxy rules with invalid targets and
throw on a matching request. The **array form** sidesteps this: each entry carries its context in
an explicit `context` field, so there are no metadata keys to misinterpret. This explanatory note
therefore lives here instead of inside the JSON.
