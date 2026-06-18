# Design Questions — backend-foundation

**No open questions remain.** All five questions (Q1–Q5) have been answered by the user and
folded into `design.md` as committed decisions:

- **Q1 — Architecture:** Modular monolith with service seams (NOT day-one microservices).
- **Q2 — Data access:** Dapper + hand-tuned SQL on hot paths; EF Core only for admin CRUD;
  schema-of-record is the versioned SQL migration scripts (EF mapped database-first, EF
  migrations disabled).
- **Q3 — Geo storage:** SQL Server `geography` type + spatial index for radius pre-filtering;
  exact distance via Haversine locally. No raw Google lat/lng; Place ID allowed.
- **Q4 — API hosts:** Separate public-API and admin-API hosts.
- **Q5 — Stack & identity:** Docker + orchestrator (Kubernetes or equivalent), config-driven
  replica scaling; Redis distributed cache (behind an abstraction); MassTransit bus with
  RabbitMQ default broker (in-process now); external OIDC identity provider (Keycloak / Auth0 /
  Entra) — no rolled-our-own token issuance.

See `design.md` (Overview, Key Components & Flows, Key Decisions & Trade-offs, Risks & Open
Questions) for the full, committed treatment.
