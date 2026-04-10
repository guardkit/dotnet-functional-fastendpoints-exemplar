---
id: TASK-W5
title: "Database + Docker — migrations, docker-compose, Keycloak realm, Dockerfile"
status: backlog
task_type: feature
wave: 5
created: 2026-04-10T00:00:00Z
updated: 2026-04-10T00:00:00Z
priority: high
complexity: 4
parent_review: TASK-59B3
feature_id: dotnet-fastendpoints-exemplar
dependencies: [TASK-W4]
tags: [dotnet, docker, keycloak, postgresql, dbup]
---

# TASK-W5: Database + Docker

## Scope

SQL migration files, `docker-compose.yml` for the full local dev stack (API + PostgreSQL 16 + Keycloak 26.4 + OTEL Collector), Keycloak realm export, `.env.example`, and `Dockerfile`.

## Deliverables

| # | Task | Output |
|---|------|--------|
| 33 | `db/migrations/0001_create_customers_schema.sql` + `0002_create_customers_table.sql` | Customers schema |
| 34 | `db/migrations/0003_create_addresses_schema.sql` + `0004_create_addresses_table.sql` | Addresses schema |
| 35 | DbUp startup wiring verified end-to-end | Auto-migration confirmed |
| 36 | `docker-compose.yml` — API + PostgreSQL 16 + Keycloak 26.4 + OTEL Collector | Full local dev stack |
| 37 | `keycloak/exemplar-realm.json` — realm export: admin+user roles, exemplar-api client, audience mapper | Reproducible KC config |
| 38 | `.env.example` — all required environment variable names with descriptions | Developer onboarding |
| 39 | `Dockerfile` — multi-stage (.NET 8 SDK build → runtime image) | Container build |

## Migration File Conventions

Files ordered by numeric prefix, applied by DbUp on startup:

```
db/migrations/
  0001_create_customers_schema.sql
  0002_create_customers_table.sql
  0003_create_addresses_schema.sql
  0004_create_addresses_table.sql
```

### Example: 0002_create_customers_table.sql
```sql
CREATE TABLE customers (
    id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name        VARCHAR(200) NOT NULL,
    email       VARCHAR(320) NOT NULL UNIQUE,
    status      VARCHAR(20)  NOT NULL DEFAULT 'Active'
                             CHECK (status IN ('Active', 'Inactive')),
    created_at  TIMESTAMPTZ  NOT NULL DEFAULT now(),
    updated_at  TIMESTAMPTZ  NOT NULL DEFAULT now()
);

CREATE INDEX ix_customers_email  ON customers (email);
CREATE INDEX ix_customers_status ON customers (status);
```

## docker-compose.yml Structure

```yaml
services:
  api:
    build: .
    ports: ["5000:8080"]
    environment:
      - ConnectionStrings__DefaultConnection=${DB_CONNECTION_STRING}
      - Authentication__Authority=${KEYCLOAK_AUTHORITY}
      - Authentication__Audience=${KEYCLOAK_CLIENT_ID}
      - OpenTelemetry__Endpoint=${OTEL_ENDPOINT}
    depends_on:
      postgres: { condition: service_healthy }
      keycloak: { condition: service_healthy }

  postgres:
    image: postgres:16-alpine
    environment:
      POSTGRES_DB:       ${POSTGRES_DB}
      POSTGRES_USER:     ${POSTGRES_USER}
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
    ports: ["5432:5432"]
    volumes: [postgres_data:/var/lib/postgresql/data]
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U ${POSTGRES_USER}"]
      interval: 5s
      timeout: 5s
      retries: 5

  keycloak:
    image: quay.io/keycloak/keycloak:26.4
    command: start-dev --import-realm
    environment:
      KC_BOOTSTRAP_ADMIN_USERNAME: admin
      KC_BOOTSTRAP_ADMIN_PASSWORD: ${KEYCLOAK_ADMIN_PASSWORD}
    volumes:
      - ./keycloak/exemplar-realm.json:/opt/keycloak/data/import/exemplar-realm.json
    ports: ["8080:8080"]
    healthcheck:
      test: ["CMD-SHELL", "curl -f http://localhost:8080/realms/exemplar/.well-known/openid-configuration"]
      interval: 10s
      timeout: 5s
      retries: 10

  otel-collector:
    image: mcr.microsoft.com/dotnet/aspire-dashboard:latest
    ports:
      - "18888:18888"   # Aspire Dashboard UI
      - "4317:18889"    # OTLP gRPC
      - "4318:18890"    # OTLP HTTP

volumes:
  postgres_data:
```

## Keycloak Realm — exemplar-realm.json

The exported realm must include:
- Realm name: `exemplar`
- Client: `exemplar-api` (confidential, audience mapper adding `exemplar-api` to `aud` claim)
- Roles: `admin`, `user` (realm roles)
- Client secret: replaced with placeholder `REPLACE_ME_CLIENT_SECRET`
- FAPI 2.0 profile not required for the exemplar (use standard client, not FAPI)

## .env.example

```bash
# PostgreSQL
POSTGRES_DB=exemplar_db
POSTGRES_USER=exemplar
POSTGRES_PASSWORD=changeme
DB_CONNECTION_STRING=Host=postgres;Database=exemplar_db;Username=exemplar;Password=changeme

# Keycloak
KEYCLOAK_ADMIN_PASSWORD=changeme
KEYCLOAK_AUTHORITY=http://localhost:8080/realms/exemplar
KEYCLOAK_CLIENT_ID=exemplar-api
KEYCLOAK_CLIENT_SECRET=REPLACE_ME_CLIENT_SECRET

# OpenTelemetry
OTEL_ENDPOINT=http://otel-collector:4317
```

## Dockerfile — multi-stage

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/Exemplar.API/Exemplar.API.csproj", "src/Exemplar.API/"]
# ... copy all project files
RUN dotnet restore "src/Exemplar.API/Exemplar.API.csproj"
COPY . .
RUN dotnet publish "src/Exemplar.API/Exemplar.API.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Exemplar.API.dll"]
```

## Quality Gates

- [ ] `docker compose up` starts cleanly, all services healthy
- [ ] `/health/ready` returns 200 after stack is up
- [ ] DbUp applies all 4 migration files idempotently (safe to run `docker compose up` twice)
- [ ] Keycloak realm imports correctly, `exemplar-api` client visible in admin UI
- [ ] `.env.example` covers every variable referenced in `docker-compose.yml`
- [ ] `REPLACE_ME_CLIENT_SECRET` is the only placeholder — no real secrets committed

## Next Wave

TASK-W6 (Integration Tests + Polish) adds TestContainers-based auth and the final template annotation pass.
