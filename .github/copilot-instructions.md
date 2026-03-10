# Copilot Workspace Instructions — IVF Information System

## Project Overview

Full-stack clinical management platform for fertility clinics. **Backend**: .NET 10 (Clean Architecture, CQRS). **Frontend**: Angular 21 (standalone components, signals). Vietnamese-language UI. Multi-tenant SaaS with HIPAA/GDPR compliance.

## Quick Commands

```bash
# Backend
dotnet restore
dotnet build
dotnet run --project src/IVF.API/IVF.API.csproj          # http://localhost:5000
dotnet test tests/IVF.Tests/IVF.Tests.csproj
dotnet test --filter "FullyQualifiedName~TestName"

# Frontend
cd ivf-client && npm install && npm start                  # http://localhost:4200
npm run build
npm test

# Infrastructure (Docker)
docker-compose up -d   # PostgreSQL:5433, Redis:6379, MinIO:9000
```

## Architecture

### Backend — 4-Layer Clean Architecture

```
IVF.Domain         → Entities, enums (no external deps)
IVF.Application    → CQRS (MediatR), FluentValidation, service interfaces
IVF.Infrastructure → EF Core/PostgreSQL, repositories, MinIO, Redis, SignServer
IVF.API            → Minimal API endpoints, JWT auth, SignalR hubs, middleware
```

Dependencies flow inward only: API → Application/Infrastructure → Domain.

### Frontend — Angular 21

```
ivf-client/src/app/
├── auth/              → Login component
├── core/
│   ├── services/      → 40+ injectable API services (providedIn: 'root')
│   ├── models/        → TypeScript interfaces matching backend DTOs
│   ├── guards/        → authGuard, guestGuard, featureGuard
│   ├── interceptors/  → auth, security, consent, tenant-limit
│   └── directives/    → HasRoleDirective
├── features/          → 22 lazy-loaded feature folders
├── layout/            → MainLayoutComponent (sidebar + router-outlet)
└── shared/            → Reusable components (search, toast, signature-pad)
```

---

## Backend Conventions

### CQRS Pattern

Every feature lives in `IVF.Application/Features/<Feature>/` with `Commands/` and `Queries/` subfolders. Commands, validators, and handlers are **colocated in the same file**.

```csharp
// Example: PatientCommands.cs contains all of these:
public record CreatePatientCommand(...) : IRequest<Result<PatientDto>>;
public class CreatePatientValidator : AbstractValidator<CreatePatientCommand> { ... }
public class CreatePatientHandler : IRequestHandler<CreatePatientCommand, Result<PatientDto>> { ... }
```

- Return `Result<T>` or `PagedResult<T>` — never raw values or `IActionResult`
- Feature gating: `[RequiresFeature(FeatureCodes.XYZ)]` attribute on commands
- Field-level access: implement `IFieldAccessProtected` on queries

### Minimal API Endpoints

Files: `src/IVF.API/Endpoints/{Feature}Endpoints.cs`

```csharp
public static class PatientEndpoints
{
    public static void MapPatientEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/patients").WithTags("Patients").RequireAuthorization();
        group.MapGet("/", async (..., IMediator m) => { ... });
        group.MapPost("/", async (CreatePatientCommand cmd, IMediator m) => { ... });
    }
}
```

- Route: `/api/{feature}` (lowercase, plural)
- Always `MapGroup()` + `.WithTags()` + `.RequireAuthorization()`
- Inject `IMediator` per-handler via lambda parameters
- Return `Results.Ok()`, `Results.NotFound()`, `Results.Created()`, etc.
- Register in Program.cs: `app.Map{Feature}Endpoints()`
- Request DTOs in `IVF.API.Contracts` namespace

### Entity Conventions

All entities extend `BaseEntity` (provides `Id`, `CreatedAt`, `UpdatedAt`, `IsDeleted`). Most implement `ITenantEntity`.

```csharp
public class Patient : BaseEntity, ITenantEntity
{
    private Patient() { }  // EF constructor
    public static Patient Create(...) => new() { ... };  // Factory method

    public string FullName { get; private set; }  // Private setters enforce encapsulation
    public Guid TenantId { get; private set; }
    public virtual ICollection<TreatmentCycle> Cycles { get; private set; } = new List<TreatmentCycle>();
}
```

- `private` parameterless ctor + `static Create()` factory
- `private set` on all properties
- Soft delete via `IsDeleted` flag + `MarkAsDeleted()`
- Collections: `virtual ICollection<T>` with default `new List<T>()`

### Dependency Injection

Per-layer extension methods in Program.cs:

```csharp
builder.Services.AddApplication();                        // MediatR, FluentValidation, pipeline behaviors
builder.Services.AddInfrastructure(builder.Configuration); // DbContext, repositories, external services
```

### MediatR Pipeline Behaviors (executed in order)

1. `ValidationBehavior` — FluentValidation
2. `FeatureGateBehavior` — Tenant feature flags
3. `VaultPolicyBehavior` — Vault policy checks
4. `ZeroTrustBehavior` — Zero Trust verification
5. `FieldAccessBehavior` — Field-level access control

### Middleware Order (matters!)

CORS → ExceptionHandler → SecurityHeaders → RateLimiter → SecurityEnforcement → VaultToken → ApiKey → Authentication → Authorization → TenantResolution → ConsentEnforcement → TokenBinding → ZeroTrust → ApiCallLogging

### Testing (Backend)

- **xUnit** + **Moq** + **FluentAssertions**
- Test naming: `{Method}_When{Condition}_Should{Expected}`
- Arrange/Act/Assert pattern
- Run: `dotnet test tests/IVF.Tests/IVF.Tests.csproj`

---

## Frontend Conventions

### Component Pattern

All components are **standalone** (`standalone: true`). No NgModules.

```typescript
@Component({
  selector: "app-patient-list",
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: "./patient-list.component.html",
  styleUrls: ["./patient-list.component.scss"],
})
export class PatientListComponent implements OnInit {
  patients = signal<Patient[]>([]); // Signals for reactive state
  loading = signal(false);
  searchQuery = ""; // Plain properties for two-way binding

  constructor(
    private patientService: PatientService,
    private router: Router,
  ) {}
}
```

- **Signals** (`signal()`, `computed()`) for component state
- **New control flow** syntax: `@if`, `@for`, `@switch` — not `*ngIf`/`*ngFor`
- File structure: `features/<feature>/<component-name>/` with `.ts`, `.html`, `.scss`
- Naming: `kebab-case` files → `PascalCase` classes
- DI: both `constructor(private svc: Service)` and `inject(Service)` are used

### Service Pattern

Each service independently injects `HttpClient` and reads `environment.apiUrl`:

```typescript
@Injectable({ providedIn: "root" })
export class PatientService {
  private http = inject(HttpClient);
  private readonly baseUrl = environment.apiUrl;

  searchPatients(
    query?: string,
    page = 1,
    pageSize = 20,
  ): Observable<PatientListResponse> {
    let params = new HttpParams().set("page", page).set("pageSize", pageSize);
    if (query) params = params.set("q", query);
    return this.http.get<PatientListResponse>(`${this.baseUrl}/patients`, {
      params,
    });
  }
}
```

- `providedIn: 'root'` (tree-shakable)
- Return `Observable<T>` from methods
- Error handling in components, not services
- No generic CRUD base class

### Routing

[app.routes.ts](../ivf-client/src/app/app.routes.ts) — single flat file with `loadComponent` for lazy loading:

```typescript
{ path: 'patients', loadComponent: () => import('./features/patients/patient-list/patient-list.component').then(m => m.PatientListComponent) }
```

- Guards: `authGuard` (layout), `guestGuard` (login), `featureGuard(code)` (tenant feature flags)
- Feature sub-routes: `loadChildren` with `FEATURE_ROUTES` export

### State Management

Pure **service + RxJS + signals** — no NgRx/NGXS.

- Signals in services for auth state (`signal()`, `computed()`, `.asReadonly()`)
- `BehaviorSubject` in services for stream-based patterns (SignalR)
- Signals in components for local UI state

### Styling

- **Tailwind CSS v4** + **SCSS** per component
- CSS custom properties (design tokens) in `src/styles.scss`
- Icons: FontAwesome Free 7
- No Material, no Bootstrap

### TypeScript

- **Strict mode fully enabled** (TS strict + Angular strict templates)
- Target: ES2022, Module: preserve
- No path aliases configured

### Formatting

- Prettier: `printWidth: 100`, `singleQuote: true`, Angular HTML parser
- No ESLint configured

---

## Key Infrastructure

| Service                | Purpose                                             | Config                                                                              |
| ---------------------- | --------------------------------------------------- | ----------------------------------------------------------------------------------- |
| **PostgreSQL**         | Primary DB                                          | `localhost:5433`, `ivf_db`                                                          |
| **Redis**              | Caching (graceful fallback)                         | `localhost:6379`                                                                    |
| **MinIO**              | Object storage (S3-compatible)                      | `localhost:9000`, buckets: `ivf-documents`, `ivf-signed-pdfs`, `ivf-medical-images` |
| **SignServer + EJBCA** | Digital PDF signing (PKI)                           | mTLS, rate-limited 30 ops/min                                                       |
| **Prometheus**         | Metrics collection & alerting                       | `127.0.0.1:9090`, external: `https://natra.site/prometheus/` (basic auth)           |
| **Grafana**            | Dashboards, unified alerting, Discord notifications | `127.0.0.1:3000`, external: `https://natra.site/grafana/` (basic auth)              |
| **Loki + Promtail**    | Log aggregation & shipping                          | `127.0.0.1:3100`, config: `docker/monitoring/`                                      |
| **Serilog**            | Structured JSON logging                             | `RenderedCompactJsonFormatter`, enriched: CorrelationId, TenantId, UserId           |

### Authentication Stack

Triple auth pipeline: `VaultToken (X-Vault-Token)` → `ApiKey (X-API-Key)` → `JWT Bearer`

- JWT: 60-min expiry, 7-day refresh token
- Frontend injects JWT via `authInterceptor`

### SignalR Hubs

- `/hubs/queue` — Real-time queue management
- `/hubs/notifications` — Push notifications
- `/hubs/fingerprint` — Biometric device events
- `/hubs/backup` — Backup/Restore real-time log streaming (used by System Restore)

### Alert Webhooks (Optional — chưa áp dụng)

> Kênh bổ sung cho **programmatic clients** có khả năng tự pull token (custom script, CI/CD, hệ thống ngoài). KHÔNG dùng cho Grafana/Prometheus (static config). Hiện tại Grafana gửi trực tiếp tới Discord qua `discord-ivf`, app-level dùng `InfrastructureMetricsPusher` → `DiscordAlertService`.

- Luồng: `GET /api/keyvault/secrets/webhooks%2Falert-token` (JWT) → `POST /api/webhooks/alerts/` (X-Webhook-Token)

- `POST /api/webhooks/alerts/grafana` — Grafana unified alerting → Discord
- `POST /api/webhooks/alerts/prometheus` — Prometheus Alertmanager → Discord
- `POST /api/webhooks/alerts/` — Generic `{source, message, level}` → Discord
- Auth: `X-Webhook-Token` header (vault token, policy `webhook-alerts`)
- Token auto-rotation: `WebhookKeyRotationService` (24h cycle, vault secret `webhooks/alert-token`)

---

## Domain Model

**Core aggregates**: `Patient`, `Couple`, `TreatmentCycle`, `User`, `Doctor`
**Treatment methods**: QHTN, IUI, ICSI, IVM
**Roles**: Admin, Doctor, Nurse, LabTech, Embryologist, Receptionist, Cashier, Pharmacist
**Dynamic forms**: `FormTemplate → FormField → FormResponse → FormFieldValue`

---

## Common Pitfalls

1. **Vietnamese UI**: All user-facing text is in Vietnamese — maintain this in new features
2. **Multi-tenancy**: Every query must respect `TenantId` — entities implement `ITenantEntity`
3. **Middleware order matters**: Auth middlewares must come before tenant resolution
4. **EF migrations**: Run from repo root with `--project` and `--startup-project` flags
5. **No path aliases**: Frontend uses relative imports — deep nesting can get verbose
6. **SignalR hardcodes `localhost:5000`** in `SignalRService` — use `environment.apiUrl` when modifying
7. **ApiService is unused**: Feature services don't extend it — each reads `environment.apiUrl` directly
8. **Docker required**: PostgreSQL, Redis, MinIO must be running via `docker-compose up -d`
9. **Auto-migration in dev**: `DatabaseSeeder.SeedAsync()` runs seeders on startup in development mode
10. **Storage tenant isolation**: All MinIO object keys must be prefixed with `TenantStoragePrefix.Prefix(tenantId, key)`. Use `StorageBuckets` constants (not hardcoded strings) for bucket names. Both in `IVF.Application.Common`.

## applyTo Patterns

When writing instructions for specific areas, scope them appropriately:

- `src/IVF.API/**` — endpoint files, middleware, Program.cs
- `src/IVF.Application/**` — CQRS handlers, validators, DTOs
- `src/IVF.Domain/**` — entities, enums, value objects
- `src/IVF.Infrastructure/**` — EF Core, repositories, external service implementations
- `ivf-client/src/app/features/**` — Angular feature components
- `ivf-client/src/app/core/**` — Angular services, models, guards, interceptors
