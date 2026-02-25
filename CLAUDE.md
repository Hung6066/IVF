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

| Layer | Project | Responsibility |
|---|---|---|
| Domain | `IVF.Domain` | Entities, enums, no external deps |
| Application | `IVF.Application` | CQRS handlers (MediatR), FluentValidation, service interfaces |
| Infrastructure | `IVF.Infrastructure` | EF Core (PostgreSQL), repositories, MinIO, Redis, SignServer |
| API | `IVF.API` | Minimal API endpoints, JWT auth, SignalR hubs, middleware |

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
- **Real-time:** Three SignalR hubs — `/hubs/queue`, `/hubs/notifications`, `/hubs/fingerprint`
- **Digital Signing:** SignServer + EJBCA PKI via mTLS. `IDigitalSigningService` in Application, implementation in Infrastructure. Rate-limited to 30 ops/min.
- **Biometrics:** DigitalPersona SDK (server-side matching, Windows only). Falls back to stub on Mac/Linux. `IBiometricMatcher` interface.
- **File Storage:** MinIO (S3-compatible). Three buckets: `ivf-documents`, `ivf-signed-pdfs`, `ivf-medical-images`.
- **Caching:** Redis via StackExchange.Redis. Degrades gracefully if unavailable.
- **PDF Generation:** QuestPDF for report/form PDF export.
- **Audit Logging:** Partitioned PostgreSQL table. Auto-creates future partitions.

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

## Additional Docs
- `/docs/form_report_builder.md` — form/report designer architecture
- `/docs/digital_signing.md` — signing infrastructure
- `/docs/matcher_infrastructure_guide.md` — biometric setup (Windows)
- `/docs/developer_guide.md` — DB schema, workflow state machines, API specs, RBAC matrix
