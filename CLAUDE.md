# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

IVF Information System — a full-stack clinical management platform for fertility clinics. Backend is .NET 10 (Clean Architecture), frontend is Angular 21 (standalone components). Supports biometric patient identification, digital PDF signing, real-time queue management, and a dynamic form/report builder.

## Commands

### Backend (.NET 10)

```bash
dotnet restore
dotnet build
dotnet run --project src/IVF.API/IVF.API.csproj
dotnet test tests/IVF.Tests/IVF.Tests.csproj

# Single test
dotnet test --filter "FullyQualifiedName~TestName"

# EF Core migrations (run from repo root)
dotnet ef migrations add <MigrationName> --project src/IVF.Infrastructure --startup-project src/IVF.API
dotnet ef database update --project src/IVF.Infrastructure --startup-project src/IVF.API
```

### Frontend (Angular 21)

```bash
cd ivf-client
npm install
npm start        # dev server on localhost:4200
npm run build    # production build
npm test         # Vitest
```

### Docker (full stack)

```bash
docker-compose up -d   # PostgreSQL:5433, Redis:6379, MinIO:9000, EJBCA:8443, SignServer:9443
```

## Architecture

### Backend — Clean Architecture

Four layers with strict dependency direction (inward only):

| Layer          | Project              | Responsibility                                                              |
| -------------- | -------------------- | --------------------------------------------------------------------------- |
| Domain         | `IVF.Domain`         | Entities, enums, no external deps                                           |
| Application    | `IVF.Application`    | CQRS handlers (MediatR), FluentValidation, service interfaces               |
| Infrastructure | `IVF.Infrastructure` | EF Core (PostgreSQL), repositories, MinIO, Redis, SignServer                |
| API            | `IVF.API`            | Minimal API endpoints, JWT/VaultToken/ApiKey auth, SignalR hubs, middleware |

**CQRS Pattern:** Every feature under `IVF.Application/Features/<Feature>/` has `Commands/` (writes) and `Queries/` (reads), each with a handler and optional FluentValidation validator. Add a MediatR pipeline behavior for cross-cutting concerns.

**Data Access:** Repository pattern + Unit of Work. `IvfDbContext` in Infrastructure. Migrations in `IVF.Infrastructure/Migrations/`. Auto-migration runs on startup in development.

**Seeding:** On startup in dev, `DatabaseSeeder`, `FlowSeeder`, `FormTemplateSeeder`, `MenuSeeder`, and `PermissionDefinitionSeeder` all run via `DatabaseSeeder.SeedAsync()`.

### Frontend — Angular 21

All components are **standalone** (no NgModules). State is service-based with RxJS (no NgRx).

```
src/app/
├── auth/          → Login
├── core/          → Services, guards, interceptors, models
│   ├── services/  → API calls, SignalR, biometrics (30+ services)
│   └── models/    → TypeScript interfaces matching backend DTOs
├── features/      → Lazy-loaded feature modules (patients, cycles, billing, forms, admin, …)
├── layout/        → Main layout wrapper with navigation
└── shared/        → Reusable components
```

Routing: feature routes are lazy-loaded in `app.routes.ts`. Guards: `authGuard` (→ login), `guestGuard` (→ dashboard).

HTTP: `ApiService` is the base HTTP client. JWT token is injected via interceptor from localStorage. Token auto-refreshes before 60-min expiry. SignalR hubs pass the JWT as `?access_token=...` query param.

### Key Infrastructure

- **Authentication:** Triple auth middleware pipeline: VaultTokenMiddleware (X-Vault-Token) → ApiKeyMiddleware (X-API-Key) → JWT Bearer. `IApiKeyValidator` validates against DB (BCrypt) with config fallback.
- **Real-time:** Three SignalR hubs — `/hubs/queue`, `/hubs/notifications`, `/hubs/fingerprint`
- **Digital Signing:** SignServer + EJBCA PKI via mTLS. `IDigitalSigningService` in Application, implementation in Infrastructure. Rate-limited to 30 ops/min.
- **Biometrics:** DigitalPersona SDK (server-side matching, Windows only). Falls back to stub on Mac/Linux. `IBiometricMatcher` interface.
- **File Storage:** MinIO (S3-compatible). Three shared buckets: `ivf-documents`, `ivf-signed-pdfs`, `ivf-medical-images`. **Tenant-isolated** via object key prefix `tenants/{tenantId}/`. Constants in `StorageBuckets`, prefix helper `TenantStoragePrefix.Prefix()` (both in `IVF.Application.Common`).
- **Caching:** Redis via StackExchange.Redis. Degrades gracefully if unavailable.
- **PDF Generation:** QuestPDF for report/form PDF export.
- **Audit Logging:** Partitioned PostgreSQL table. Auto-creates future partitions.
- **Monitoring:** Prometheus + Grafana + Loki + Promtail. All ports `127.0.0.1` only. Grafana at `https://natra.site/grafana/`, Prometheus at `https://natra.site/prometheus/` (basic auth). MinIO Console via SSH tunnel. 6 scrape targets, 31 Prometheus alert rules, 9 Loki log-based alerts, 25 Grafana unified alert rules, 4 Grafana dashboards. Discord notifications via `discord-ivf` contact point. Config: `docker/monitoring/`. See `docs/infrastructure_operations_guide.md`.
- **Alert Webhooks (optional, chưa áp dụng):** `/api/webhooks/alerts/{grafana|prometheus|}` — kênh bổ sung tùy chọn cho **programmatic clients** có khả năng tự pull token (custom script, CI/CD, hệ thống ngoài). KHÔNG dùng cho Grafana/Prometheus (static config). Luồng: `GET /api/keyvault/secrets/webhooks%2Falert-token` (JWT) → `POST /api/webhooks/alerts/` (X-Webhook-Token). Hiện tại Grafana gửi trực tiếp tới Discord qua `discord-ivf`, app-level dùng `InfrastructureMetricsPusher` → `DiscordAlertService`. Token auto-rotated mỗi 24h bởi `WebhookKeyRotationService`.
- **Structured Logging:** Serilog with `RenderedCompactJsonFormatter` (JSON). Enriched with `CorrelationId`, `TenantId`, `UserId`, `MachineName`, `Environment`, `ProcessId`, `ThreadId`. Middleware: `CorrelationIdMiddleware`, `LogContextEnrichmentMiddleware`. MediatR: `LoggingBehavior`. Promtail extracts Serilog `@l` (level) and `@mt` (message template) via regex.

### API Conventions

- Endpoints at `/api/<feature>` (minimal API pattern)
- Paged responses: `{ items: [...], totalCount, page, pageSize }`
- Validation errors → 400 via exception middleware
- JWT expiry: 60 min; refresh token: 7 days
- Rate limiting: 100 req/min global

## Key Configuration

`src/IVF.API/appsettings.json` — connection strings, JWT, MinIO, DigitalSigning settings.
`src/IVF.API/appsettings.Development.json` — dev logging, desktop client API keys.
`ivf-client/src/environments/` — Angular environment files.

Default dev DB: `Host=localhost;Port=5433;Database=ivf_db;Username=postgres;Password=postgres`

## Domain Model Highlights

Core aggregates: `Patient`, `Couple`, `TreatmentCycle`, `User`, `Doctor`.
Treatment methods: `QHTN`, `IUI`, `ICSI`, `IVM`.
Roles: Admin, Doctor, Nurse, LabTech, Embryologist, Receptionist, Cashier, Pharmacist.
Forms system: `FormTemplate → FormField → FormResponse → FormFieldValue` supports dynamic clinical forms and report templates.
Enterprise User Management: `UserSession`, `UserGroup`, `UserGroupMember`, `UserGroupPermission`, `UserLoginHistory`, `UserConsent` — enterprise-grade session management, group-based IAM, login analytics, GDPR/HIPAA consent.
Enterprise Security: `ConditionalAccessPolicy`, `SecurityIncident`, `IncidentResponseRule`, `DataRetentionPolicy`, `ImpersonationRequest`, `PermissionDelegation`, `UserBehaviorProfile`, `NotificationPreference`, `SecurityEvent` — Zero Trust, conditional access, incident response automation, behavioral analytics, impersonation (RFC 8693), permission delegation, data retention (HIPAA/GDPR), security notifications.

## Additional Docs

- `/docs/advanced_security.md` — advanced security module (Passkeys, MFA, TOTP, SMS OTP, rate limiting, geo-fencing, threats, lockouts, IP whitelist)
- `/docs/enterprise_security.md` — enterprise security module (conditional access, incident response, data retention, impersonation, permission delegation, behavioral analytics, security notifications, 35 API endpoints)
- `/docs/enterprise_user_management.md` — enterprise user management (sessions, groups, IAM, login analytics, risk detection, GDPR/HIPAA consent)
- `/docs/infrastructure_operations_guide.md` — monitoring stack, data retention, read-replica routing, auto-healing, disaster recovery
- `/docs/deployment_operations_guide.md` — deployment, CI/CD, rolling updates, incident response
- `/docs/ci_cd_deploy_guide.md` — GitHub Actions CI/CD pipeline, Discord notifications
- `/docs/form_report_builder.md` — form/report designer architecture
- `/docs/digital_signing.md` — signing infrastructure
- `/docs/cloud_kms_deployment_guide.md` — Cloud KMS/HSM deployment (AWS/Azure/GCP)
- `/docs/matcher_infrastructure_guide.md` — biometric setup (Windows)
- `/docs/developer_guide.md` — DB schema, workflow state machines, API specs, RBAC matrix
