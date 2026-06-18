# Deploy — WhereToEat / ДеПоїсти local dev stack (Step 13)

Container assets for running the whole backend locally: SQL Server, Redis, RabbitMQ, a Keycloak dev
IdP, and the three hosts (public API, admin API, worker).

## Contents

| File | Purpose |
| --- | --- |
| `Dockerfile.PublicApi` | Public API host image (multi-stage SDK → ASP.NET runtime). |
| `Dockerfile.AdminApi` | Admin API host image (separate process, security isolation). |
| `Dockerfile.Worker` | Worker host image (Quartz clustered scheduler; runtime image). |
| `docker-compose.yml` | The full local stack + a one-shot `migrations` service. |
| `.env.example` | Dev config + **config-driven replica/resource scaling knobs**. |
| `keycloak/realm-export.json` | Dev realm: `admin` + `user` roles/scopes, the `wheretoeat-api` client, an admin test user. |

## Run it

All commands are run **from the repo root** (the Docker build context is the repo root so the central
`Directory.Build.props` / `Directory.Packages.props` and every referenced project resolve).

```bash
# 1. Copy the example env and adjust secrets if you like.
cp deploy/.env.example deploy/.env

# 2. Build images + bring the stack up.
docker compose -f deploy/docker-compose.yml --env-file deploy/.env up --build
```

The `migrations` one-shot service applies the schema-of-record SQL scripts before the hosts start; the
worker also re-applies them idempotently on its own startup (it needs the Quartz cluster tables).

## Smoke check (after the stack is up)

```bash
curl http://localhost:8080/health     # public API  (PUBLIC_API_PORT)
curl http://localhost:8081/health     # admin API   (ADMIN_API_PORT)
# Keycloak admin console:  http://localhost:8088  (admin / admin by default)
# RabbitMQ management UI:   http://localhost:15672 (guest / guest by default)
```

## Scaling knobs (env-driven)

Set in `deploy/.env`:

- `PUBLIC_API_REPLICAS`, `ADMIN_API_REPLICAS`, `WORKER_REPLICAS` — replica counts.
- `*_CPU_LIMIT`, `*_MEM_LIMIT` — per-service resource limits.

The **worker is safe to scale**: the clustered Quartz store makes each scheduled trigger fire on
exactly one replica.

```bash
# Example: run two workers (one scheduled trigger still fires on exactly one of them).
WORKER_REPLICAS=2 docker compose -f deploy/docker-compose.yml --env-file deploy/.env up --build
```

## Messaging transport

`MESSAGING_TRANSPORT=RabbitMq` (the default in `.env.example`) points MassTransit at the compose
broker. Set it to `InMemory` to run the hosts without the broker — switching is a config change only.

> Note: `docker compose up` is a **manual smoke**, not a CI gate. The automated tests use Testcontainers
> and do not require this compose stack.
