# IVF System — Developer Guide

> **IVF Information System** — A full-stack clinical management platform for fertility clinics.
> Backend: .NET 10 (Clean Architecture) · Frontend: Angular 21 (Standalone Components) · Database: PostgreSQL 16

---

## Table of Contents

1. [Quick Start](#1-quick-start)
2. [Project Structure](#2-project-structure)
3. [Architecture](#3-architecture)
4. [Backend Development](#4-backend-development)
5. [Frontend Development](#5-frontend-development)
6. [Domain Model](#6-domain-model)
7. [Authentication & Authorization](#7-authentication--authorization)
8. [Real-Time Communication (SignalR)](#8-real-time-communication-signalr)
9. [Infrastructure Services](#9-infrastructure-services)
10. [Database & Migrations](#10-database--migrations)
11. [Testing](#11-testing)
12. [Docker & Deployment](#12-docker--deployment)
13. [Configuration Reference](#13-configuration-reference)
14. [Coding Conventions](#14-coding-conventions)
15. [Adding a New Feature (Step-by-Step)](#15-adding-a-new-feature-step-by-step)
16. [Troubleshooting](#16-troubleshooting)

---

## 1. Quick Start

### Prerequisites

| Tool                    | Version | Purpose                                     |
| ----------------------- | ------- | ------------------------------------------- |
| .NET SDK                | 10.0+   | Backend runtime                             |
| Node.js                 | 22+     | Frontend tooling                            |
| Docker & Docker Compose | Latest  | PostgreSQL, Redis, MinIO, EJBCA, SignServer |
| Git                     | Latest  | Source control                              |

### First-Time Setup

```bash
# 1. Clone the repository
git clone <repo-url> && cd IVF

# 2. Start infrastructure (PostgreSQL, Redis, MinIO, EJBCA, SignServer)
docker-compose up -d

# 3. Restore and run the backend
dotnet restore
dotnet run --project src/IVF.API/IVF.API.csproj
# API available at http://localhost:5000

# 4. In a separate terminal — install and run the frontend
cd ivf-client
npm install
npm start
# Angular dev server at http://localhost:4200
```

### Default Credentials

| Service          | URL                                           | Username     | Password      |
| ---------------- | --------------------------------------------- | ------------ | ------------- |
| IVF API          | `http://localhost:5000`                       | admin        | Admin@123     |
| PostgreSQL       | `localhost:5433`                              | postgres     | postgres      |
| MinIO Console    | `http://localhost:9001`                       | minioadmin   | minioadmin123 |
| EJBCA Admin      | `https://localhost:8443/ejbca/adminweb/`      | (cert-based) | —             |
| SignServer Admin | `https://localhost:9443/signserver/adminweb/` | (cert-based) | —             |
| Redis            | `localhost:6379`                              | —            | —             |

### Useful Commands

```bash
# Backend
dotnet build                                         # Build all projects
dotnet test tests/IVF.Tests/IVF.Tests.csproj         # Run tests
dotnet test --filter "FullyQualifiedName~TestName"   # Run single test
dotnet watch run --project src/IVF.API/IVF.API.csproj  # Dev with hot reload

# Frontend
cd ivf-client
npm start          # Dev server with HMR
npm run build      # Production build
npm test           # Run Vitest tests

# EF Core Migrations
dotnet ef migrations add <Name> --project src/IVF.Infrastructure --startup-project src/IVF.API
dotnet ef database update --project src/IVF.Infrastructure --startup-project src/IVF.API
```

---

## 2. Project Structure

```
IVF/
├── src/
│   ├── IVF.Domain/              ← Domain layer (entities, enums, no dependencies)
│   │   ├── Entities/            ← 61 entity classes
│   │   └── Enums/               ← 7 enum files
│   │
│   ├── IVF.Application/         ← Application layer (CQRS, validation, interfaces)
│   │   ├── Features/            ← 16 feature folders (Commands/ + Queries/)
│   │   │   ├── Patients/
│   │   │   ├── Couples/
│   │   │   ├── Cycles/
│   │   │   ├── Billing/
│   │   │   ├── Forms/
│   │   │   ├── Queue/
│   │   │   └── ... (16 total)
│   │   ├── Common/
│   │   │   ├── Behaviors/       ← MediatR pipeline (ValidationBehavior)
│   │   │   ├── Interfaces/      ← 35 repository + service contracts
│   │   │   └── Result.cs        ← Result<T>, Result, PagedResult<T>
│   │   └── DependencyInjection.cs
│   │
│   ├── IVF.Infrastructure/      ← Infrastructure layer (EF Core, repos, services)
│   │   ├── Persistence/
│   │   │   ├── IvfDbContext.cs  ← 60+ DbSets
│   │   │   ├── Configurations/  ← 60 entity configurations
│   │   │   ├── Migrations/      ← 24+ migrations
│   │   │   └── *Seeder.cs       ← Database seeders
│   │   ├── Repositories/        ← 29 repository implementations
│   │   ├── Services/            ← 28 service implementations
│   │   └── DependencyInjection.cs
│   │
│   ├── IVF.API/                 ← API layer (endpoints, hubs, middleware)
│   │   ├── Endpoints/           ← 36 endpoint files (Minimal API)
│   │   ├── Hubs/                ← 4 SignalR hubs + auth filter
│   │   ├── Services/            ← API-level services (backup, CA, PDF)
│   │   ├── Program.cs           ← Application bootstrap (~400 lines)
│   │   └── appsettings.json
│   │
│   ├── IVF.FingerprintClient/   ← Desktop fingerprint client (Windows)
│   └── IVF.Gateway/             ← API Gateway (future)
│
├── ivf-client/                  ← Angular 21 frontend
│   └── src/app/
│       ├── auth/                ← Login page
│       ├── core/
│       │   ├── guards/          ← authGuard, guestGuard
│       │   ├── interceptors/    ← JWT auth interceptor
│       │   ├── models/          ← 16 TypeScript interface files
│       │   └── services/        ← 31 service files
│       ├── features/            ← 19 lazy-loaded feature modules
│       │   ├── patients/
│       │   ├── cycles/
│       │   ├── forms/           ← Dynamic form/report builder
│       │   ├── admin/           ← Admin panel (10 sub-features)
│       │   ├── queue/           ← Real-time queue (SignalR)
│       │   └── ... (19 total)
│       ├── layout/              ← Main layout with navigation
│       └── shared/              ← Reusable components
│
├── tests/IVF.Tests/             ← Unit tests (xUnit)
├── docs/                        ← Documentation
├── docker-compose.yml           ← Full infrastructure
├── certs/                       ← TLS certificates
├── secrets/                     ← Sensitive config (gitignored)
└── scripts/                     ← Setup & deployment scripts
```

---

## 3. Architecture

### Clean Architecture (Backend)

The backend follows **Clean Architecture** with strict dependency direction — outer layers depend on inner layers, never the reverse.

```
┌─────────────────────────────────────────────────┐
│                  IVF.API                         │
│  Endpoints · Hubs · Middleware · Program.cs      │
│  ┌───────────────────────────────────────────┐  │
│  │          IVF.Infrastructure                │  │
│  │  EF Core · Repositories · Services        │  │
│  │  ┌───────────────────────────────────┐    │  │
│  │  │       IVF.Application              │    │  │
│  │  │  CQRS Handlers · Validators       │    │  │
│  │  │  ┌───────────────────────────┐    │    │  │
│  │  │  │      IVF.Domain            │    │    │  │
│  │  │  │  Entities · Enums          │    │    │  │
│  │  │  └───────────────────────────┘    │    │  │
│  │  └───────────────────────────────────┘    │  │
│  └───────────────────────────────────────────┘  │
└─────────────────────────────────────────────────┘
```

| Layer              | Project              | Depends on                  | Responsibility                                                |
| ------------------ | -------------------- | --------------------------- | ------------------------------------------------------------- |
| **Domain**         | `IVF.Domain`         | Nothing                     | Entities, enums, domain logic                                 |
| **Application**    | `IVF.Application`    | Domain                      | CQRS handlers (MediatR), FluentValidation, service interfaces |
| **Infrastructure** | `IVF.Infrastructure` | Application, Domain         | EF Core, PostgreSQL, MinIO, Redis, SignServer implementations |
| **API**            | `IVF.API`            | Application, Infrastructure | HTTP endpoints, SignalR hubs, JWT auth, middleware            |

### Request Flow

```
HTTP Request
  │
  ▼
Minimal API Endpoint (IVF.API/Endpoints/)
  │
  ├── IMediator.Send(Command/Query)
  │     │
  │     ▼
  │   ValidationBehavior (MediatR Pipeline)
  │     │ Runs FluentValidation validators
  │     │ Throws ValidationException on failure → 400
  │     ▼
  │   CommandHandler / QueryHandler (IVF.Application/Features/)
  │     │ Uses IRepository interfaces
  │     │ Returns Result<T> or PagedResult<T>
  │     ▼
  │   Repository Implementation (IVF.Infrastructure/Repositories/)
  │     │ EF Core queries against IvfDbContext
  │     ▼
  │   IUnitOfWork.SaveChangesAsync()
  │     │ AuditInterceptor runs (auto-set CreatedAt/UpdatedAt)
  │     ▼
  │   PostgreSQL
  │
  ▼
HTTP Response (200 OK / 201 Created / 400 Bad Request / 404 Not Found)
```

### Frontend Architecture (Angular 21)

- **All components are standalone** (no NgModules)
- **State management**: Service-based with RxJS (no NgRx)
- **Routing**: Lazy-loaded via `loadComponent`/`loadChildren` in `app.routes.ts`
- **HTTP**: `ApiService` as base client, JWT injected via `authInterceptor`
- **Real-time**: SignalR for queue updates, notifications, biometrics, backup progress

---

## 4. Backend Development

### 4.1 CQRS Pattern

Every feature is in `IVF.Application/Features/<Feature>/` with `Commands/` (writes) and `Queries/` (reads).

**Command (write):**

```csharp
// 1. Define the command
public record CreatePatientCommand(
    string FullName,
    DateTime DateOfBirth,
    Gender Gender,
    PatientType PatientType,
    string? Phone,
    string? Address
) : IRequest<Result<PatientDto>>;

// 2. Add validation
public class CreatePatientValidator : AbstractValidator<CreatePatientCommand>
{
    public CreatePatientValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.DateOfBirth).LessThan(DateTime.Today);
    }
}

// 3. Implement the handler
public class CreatePatientHandler(
    IPatientRepository patientRepo,
    IUnitOfWork unitOfWork
) : IRequestHandler<CreatePatientCommand, Result<PatientDto>>
{
    public async Task<Result<PatientDto>> Handle(
        CreatePatientCommand request, CancellationToken ct)
    {
        var code = await patientRepo.GenerateCodeAsync(ct);
        var patient = Patient.Create(code, request.FullName, request.DateOfBirth, ...);

        await patientRepo.AddAsync(patient, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result<PatientDto>.Success(PatientDto.FromEntity(patient));
    }
}
```

**Query (read):**

```csharp
public record SearchPatientsQuery(
    string? SearchTerm, string? Gender,
    int Page = 1, int PageSize = 20
) : IRequest<PagedResult<PatientDto>>;

public class SearchPatientsHandler(
    IPatientRepository patientRepo
) : IRequestHandler<SearchPatientsQuery, PagedResult<PatientDto>>
{
    public async Task<PagedResult<PatientDto>> Handle(
        SearchPatientsQuery request, CancellationToken ct)
    {
        var (items, total) = await patientRepo.SearchAsync(
            request.SearchTerm, request.Gender,
            request.Page, request.PageSize, ct);

        var dtos = items.Select(PatientDto.FromEntity).ToList();
        return new PagedResult<PatientDto>(dtos, total, request.Page, request.PageSize);
    }
}
```

### 4.2 Result Types

All handlers return one of three result types:

```csharp
// Success with data
Result<T>.Success(value)        // IsSuccess=true, Value=value
Result<T>.Failure("not found")  // IsSuccess=false, Error="not found"

// Success without data (e.g., delete)
Result.Success()
Result.Failure("error message")

// Paginated results
new PagedResult<T>(items, totalCount, page, pageSize)
// Response shape: { items: [...], totalCount, page, pageSize, totalPages }
```

### 4.3 Minimal API Endpoints

Endpoints are defined as static extension methods in `IVF.API/Endpoints/`:

```csharp
public static class PatientEndpoints
{
    public static void MapPatientEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/patients")
            .WithTags("Patients")
            .RequireAuthorization();

        group.MapGet("/", async (IMediator m, string? q, int page = 1, int pageSize = 20) =>
            Results.Ok(await m.Send(new SearchPatientsQuery(q, null, page, pageSize))));

        group.MapGet("/{id:guid}", async (Guid id, IMediator m) =>
        {
            var r = await m.Send(new GetPatientByIdQuery(id));
            return r.IsSuccess ? Results.Ok(r.Value) : Results.NotFound(r.Error);
        });

        group.MapPost("/", async (CreatePatientCommand cmd, IMediator m) =>
        {
            var r = await m.Send(cmd);
            return r.IsSuccess
                ? Results.Created($"/api/patients/{r.Value!.Id}", r.Value)
                : Results.BadRequest(r.Error);
        });

        group.MapPut("/{id:guid}", async (Guid id, UpdatePatientRequest req, IMediator m) =>
        {
            var r = await m.Send(new UpdatePatientCommand(id, req.FullName, ...));
            return r.IsSuccess ? Results.Ok(r.Value) : Results.NotFound(r.Error);
        });

        group.MapDelete("/{id:guid}", async (Guid id, IMediator m) =>
        {
            var r = await m.Send(new DeletePatientCommand(id));
            return r.IsSuccess ? Results.NoContent() : Results.NotFound(r.Error);
        });
    }
}
```

Register in `Program.cs`:

```csharp
app.MapPatientEndpoints();
```

### 4.4 Repository Pattern

Repositories live in `IVF.Infrastructure/Repositories/` and implement interfaces from `IVF.Application/Common/Interfaces/`.

```csharp
// Interface (Application layer)
public interface IPatientRepository
{
    Task<Patient?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<(IReadOnlyList<Patient> Items, int Total)> SearchAsync(
        string? query, string? gender, int page, int pageSize, CancellationToken ct = default);
    Task<Patient> AddAsync(Patient patient, CancellationToken ct = default);
    Task UpdateAsync(Patient patient, CancellationToken ct = default);
    Task<string> GenerateCodeAsync(CancellationToken ct = default);
}

// Implementation (Infrastructure layer)
public class PatientRepository(IvfDbContext context) : IPatientRepository
{
    public async Task<Patient?> GetByIdAsync(Guid id, CancellationToken ct)
        => await context.Patients.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<(IReadOnlyList<Patient> Items, int Total)> SearchAsync(
        string? query, string? gender, int page, int pageSize, CancellationToken ct)
    {
        var q = context.Patients.AsNoTracking().AsQueryable();

        if (!string.IsNullOrEmpty(query))
            q = q.Where(p => p.FullName.Contains(query) || p.PatientCode.Contains(query));

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }
}
```

**Key patterns:**

- `AsNoTracking()` for all read queries (performance)
- No `SaveChanges` in repositories — delegated to `IUnitOfWork`
- All methods accept `CancellationToken`

### 4.5 Entity Configuration (EF Core)

Each entity gets a Fluent API configuration in `IVF.Infrastructure/Persistence/Configurations/`:

```csharp
public class PatientConfiguration : IEntityTypeConfiguration<Patient>
{
    public void Configure(EntityTypeBuilder<Patient> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.PatientCode).IsRequired().HasMaxLength(20);
        builder.HasIndex(p => p.PatientCode).IsUnique();
        builder.Property(p => p.FullName).IsRequired().HasMaxLength(200);
        builder.Property(p => p.Gender).HasConversion<string>();
    }
}
```

### 4.6 Validation Pipeline

FluentValidation runs automatically via MediatR's `ValidationBehavior`:

```
Request → ValidationBehavior → [AbstractValidator<TRequest>] → Handler
                                    │
                                    │ On failure:
                                    ▼
                            ValidationException
                                    │
                                    ▼
                        Exception Middleware → 400 BadRequest
                        { errors: ["FullName must not be empty", ...] }
```

Validators are auto-discovered from the Application assembly:

```csharp
services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
```

---

## 5. Frontend Development

### 5.1 Standalone Components

All components use Angular's standalone pattern (no NgModules):

```typescript
@Component({
  selector: "app-patient-list",
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: "./patient-list.component.html",
  styleUrls: ["./patient-list.component.scss"],
})
export class PatientListComponent implements OnInit {
  patients = signal<Patient[]>([]);
  loading = signal(false);

  constructor(
    private patientService: PatientService,
    private router: Router,
  ) {}

  ngOnInit() {
    this.loadPatients();
  }

  loadPatients() {
    this.loading.set(true);
    this.patientService.search().subscribe({
      next: (data) => this.patients.set(data.items),
      complete: () => this.loading.set(false),
    });
  }
}
```

### 5.2 Routing (Lazy Loading)

Routes are defined in `app.routes.ts` using lazy loading for code splitting:

```typescript
export const routes: Routes = [
  {
    path: "login",
    loadComponent: () =>
      import("./auth/login/login.component").then((m) => m.LoginComponent),
    canActivate: [guestGuard],
  },
  {
    path: "",
    loadComponent: () =>
      import("./layout/main-layout/main-layout.component").then(
        (m) => m.MainLayoutComponent,
      ),
    canActivate: [authGuard],
    children: [
      {
        path: "dashboard",
        loadComponent: () =>
          import("./features/dashboard/dashboard.component").then(
            (m) => m.DashboardComponent,
          ),
      },
      {
        path: "patients",
        loadComponent: () =>
          import("./features/patients/patient-list/patient-list.component").then(
            (m) => m.PatientListComponent,
          ),
      },
      {
        path: "patients/:id",
        loadComponent: () =>
          import("./features/patients/patient-detail/patient-detail.component").then(
            (m) => m.PatientDetailComponent,
          ),
      },
      // Sub-routes with loadChildren for complex features
      {
        path: "forms",
        loadChildren: () =>
          import("./features/forms/forms.routes").then((m) => m.FORMS_ROUTES),
      },
      // ... more routes
      { path: "", redirectTo: "dashboard", pathMatch: "full" },
    ],
  },
];
```

### 5.3 Services (HTTP + State)

Services handle API calls and maintain component state:

```typescript
@Injectable({ providedIn: "root" })
export class PatientService {
  private readonly baseUrl = `${environment.apiUrl}/patients`;

  constructor(private http: HttpClient) {}

  search(
    query?: string,
    page = 1,
    pageSize = 20,
  ): Observable<PagedResult<Patient>> {
    const params = new HttpParams().set("page", page).set("pageSize", pageSize);
    if (query) params.set("q", query);
    return this.http.get<PagedResult<Patient>>(this.baseUrl, { params });
  }

  getById(id: string): Observable<Patient> {
    return this.http.get<Patient>(`${this.baseUrl}/${id}`);
  }

  create(patient: CreatePatientRequest): Observable<Patient> {
    return this.http.post<Patient>(this.baseUrl, patient);
  }

  update(id: string, patient: UpdatePatientRequest): Observable<Patient> {
    return this.http.put<Patient>(`${this.baseUrl}/${id}`, patient);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}
```

### 5.4 Auth Interceptor

JWT is automatically injected by the HTTP interceptor (`core/interceptors/auth.interceptor.ts`):

```typescript
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);
  const token = authService.getToken();

  // Skip for login/refresh endpoints
  if (req.url.includes("/auth/login") || req.url.includes("/auth/refresh"))
    return next(req);

  // Attach JWT Bearer token
  if (token)
    req = req.clone({ setHeaders: { Authorization: `Bearer ${token}` } });

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status === 401 && token) {
        // Auto-refresh on 401
        return authService.refreshToken().pipe(
          switchMap(() => {
            const newReq = req.clone({
              setHeaders: { Authorization: `Bearer ${authService.getToken()}` },
            });
            return next(newReq);
          }),
          catchError(() => {
            authService.logout(); // Redirect to login
            return throwError(() => error);
          }),
        );
      }
      return throwError(() => error);
    }),
  );
};
```

### 5.5 Guards

```typescript
// authGuard — redirects unauthenticated users to /login
export const authGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const router = inject(Router);
  if (authService.isAuthenticated()) return true;
  router.navigate(["/login"]);
  return false;
};

// guestGuard — redirects authenticated users to /dashboard
export const guestGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const router = inject(Router);
  if (!authService.isAuthenticated()) return true;
  router.navigate(["/dashboard"]);
  return false;
};
```

### 5.6 Feature Modules Summary

| Feature     | Path           | Description                                               |
| ----------- | -------------- | --------------------------------------------------------- |
| Dashboard   | `/dashboard`   | Overview with stats & quick actions                       |
| Patients    | `/patients`    | Patient registration, search, biometrics, documents       |
| Couples     | `/couples`     | Couple management (wife + husband linkage)                |
| Cycles      | `/cycles/:id`  | Treatment cycle tracking (phases, outcomes)               |
| Queue       | `/queue/:dept` | Real-time reception queue (SignalR)                       |
| Forms       | `/forms/*`     | Dynamic form builder, renderer, reports (12 sub-routes)   |
| Billing     | `/billing`     | Invoices, payments, prescriptions                         |
| Ultrasounds | `/ultrasounds` | Ultrasound imaging & follicle tracking                    |
| Andrology   | `/andrology`   | Semen analysis & sperm processing                         |
| Sperm Bank  | `/sperm-bank`  | Donor management, sample inventory                        |
| Lab         | `/lab`         | Lab test results                                          |
| Reports     | `/reports`     | Clinical report generation                                |
| Pharmacy    | `/pharmacy`    | Drug inventory & dispensing                               |
| Admin       | `/admin`       | Users, permissions, backup, certificates, digital signing |

---

## 6. Domain Model

### Core Aggregates

```
Patient ──┐
           ├── Couple ──── TreatmentCycle
Patient ──┘                     │
                                ├── Ultrasound
                                ├── SemenAnalysis
                                ├── Embryo
                                ├── StimulationData
                                ├── CultureData
                                ├── TransferData
                                ├── LutealPhaseData
                                ├── PregnancyData
                                ├── BirthData
                                ├── AdverseEventData
                                └── Invoice
```

### Treatment Methods

| Code | Name                             | Description                 |
| ---- | -------------------------------- | --------------------------- |
| QHTN | Quan hệ tự nhiên                 | Natural intercourse timing  |
| IUI  | Intrauterine Insemination        | Sperm injection into uterus |
| ICSI | Intracytoplasmic Sperm Injection | Single sperm into egg       |
| IVM  | In Vitro Maturation              | Immature egg maturation     |

### Cycle Phases

```
Consultation → OvarianStimulation → TriggerShot → EggRetrieval
     → EmbryoCulture → EmbryoTransfer → LutealSupport → PregnancyTest → Completed
```

### User Roles (RBAC)

| Role         | Access Level                                |
| ------------ | ------------------------------------------- |
| Admin        | Full system access                          |
| Director     | View all data, manage reports               |
| Doctor       | Patients, cycles, ultrasound, prescriptions |
| Embryologist | Embryo culture, grading, cryopreservation   |
| Nurse        | Assist doctor, manage queue, injections     |
| LabTech      | Lab test results, semen analysis            |
| Receptionist | Patient intake, queue management            |
| Cashier      | Billing, payments                           |
| Pharmacist   | Prescription dispensing                     |

### Permissions (Fine-Grained)

35 permissions across 12 modules: `ViewPatients`, `ManagePatients`, `ViewCycles`, `ManageCycles`, `PerformUltrasound`, `ManageEmbryos`, `CreateInvoice`, `ProcessPayment`, `CallTicket`, `DesignForms`, `SignDocuments`, `ManageSystem`, `ViewAuditLog`, etc.

### Key Enums

```csharp
Gender          → Male, Female
PatientType     → Infertility, EggDonor, SpermDonor
QueueType       → Reception, Consultation, Ultrasound, LabTest, Andrology, Pharmacy, Injection
TicketStatus    → Waiting, Called, InService, Completed, Skipped, Cancelled
TicketPriority  → Normal, VIP, Emergency
EmbryoGrade     → AA, AB, BA, BB, AC, CA, BC, CB, CC, CD, DC, DD
EmbryoDay       → D1, D2, D3, D4, D5, D6
EmbryoStatus    → Developing, Transferred, Frozen, Thawed, Discarded, Arrested
SpecimenType    → Embryo, Sperm, Oocyte
InvoiceStatus   → Draft, Issued, PartiallyPaid, Paid, Refunded, Cancelled
DonorStatus     → Screening, Active, Suspended, Retired, Inactive, Rejected
```

### Forms System

Dynamic clinical forms and reports:

```
FormTemplate (name, category, layout)
  └── FormField (label, type, required, options, validation, layout)
        └── FormFieldValue (response data per field)
              └── FormFieldValueDetail (repeating data cells)

FormResponse (patient link, template link, submitted data)
  └── FormFieldValue[]

ReportTemplate → FormTemplate with report-specific rendering config
```

Supports 20+ field types including text, number, date, dropdown, checkbox, radio, table, image, rich text, concept lookup, and linked fields.

---

## 7. Authentication & Authorization

### JWT Configuration

```json
{
  "JwtSettings": {
    "Secret": "IVF-System-Super-Secret-Key-For-JWT-Token-Generation-2026",
    "Issuer": "IVF-System",
    "Audience": "IVF-Users",
    "ExpiryMinutes": 60,
    "RefreshTokenExpiryDays": 7
  }
}
```

### Auth Flow

```
1. POST /api/auth/login { username, password }
   → Returns { token, refreshToken, expiresAt, user }

2. All API calls include: Authorization: Bearer <token>

3. Token expires in 60 minutes
   → Frontend auto-refreshes via interceptor on 401

4. Refresh token valid for 7 days
   → POST /api/auth/refresh { refreshToken }

5. SignalR passes token via query string:
   /hubs/queue?access_token=<JWT>
```

### Authorization Policies

```csharp
// In Program.cs
builder.Services.AddAuthorization(opts =>
{
    opts.AddPolicy("AdminOnly", p => p.RequireRole("Admin"));
    opts.AddPolicy("MedicalStaff", p => p.RequireRole("Admin", "Doctor", "Nurse", "Embryologist"));
    opts.AddPolicy("LabAccess", p => p.RequireRole("Admin", "LabTech", "Embryologist"));
    // ... 8+ policies
});

// Usage in endpoints
group.RequireAuthorization();           // Any authenticated user
group.RequireAuthorization("AdminOnly"); // Admin only
```

### Rate Limiting

```
Global:     100 requests/minute per client
Signing:     30 operations/minute (digital signing)
```

---

## 8. Real-Time Communication (SignalR)

### Hubs

| Hub             | Endpoint              | Purpose                                      |
| --------------- | --------------------- | -------------------------------------------- |
| QueueHub        | `/hubs/queue`         | Queue ticket updates (call, complete, stats) |
| NotificationHub | `/hubs/notifications` | Push notifications to users                  |
| FingerprintHub  | `/hubs/fingerprint`   | Biometric matching progress                  |
| BackupHub       | `/hubs/backup`        | Backup/restore/deploy progress               |

### Client Connection (Angular)

```typescript
import * as signalR from "@microsoft/signalr";

const connection = new signalR.HubConnectionBuilder()
  .withUrl(`${environment.apiUrl.replace("/api", "")}/hubs/queue`, {
    accessTokenFactory: () => this.authService.getToken(),
  })
  .withAutomaticReconnect()
  .build();

// Listen for events
connection.on("TicketCalled", (ticket) => {
  /* update UI */
});
connection.on("DepartmentStats", (stats) => {
  /* refresh stats */
});

// Start connection
await connection.start();

// Join a group
await connection.invoke("JoinDepartment", departmentCode);
```

### Server-Side (Hub)

```csharp
public class QueueHub : Hub
{
    public async Task JoinDepartment(string departmentCode)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"dept_{departmentCode}");
    }
}

// Sending from any service
public class QueueNotifier(IHubContext<QueueHub> hub)
{
    public async Task NotifyTicketCalled(string dept, QueueTicket ticket)
    {
        await hub.Clients.Group($"dept_{dept}")
            .SendAsync("TicketCalled", ticket);
    }
}
```

---

## 9. Infrastructure Services

### 9.1 File Storage (MinIO / S3)

Three buckets:

- `ivf-documents` — Patient documents, uploads
- `ivf-signed-pdfs` — Digitally signed PDF reports
- `ivf-medical-images` — Ultrasound images, photos

```csharp
// Interface
public interface IObjectStorageService
{
    Task<string> UploadAsync(string bucket, string objectName, Stream data, string contentType);
    Task<Stream> DownloadAsync(string bucket, string objectName);
    Task DeleteAsync(string bucket, string objectName);
    Task<bool> ExistsAsync(string bucket, string objectName);
}
```

### 9.2 Digital Signing (SignServer + EJBCA)

- mTLS client authentication via P12 certificate
- PDF signing with visible signature stamp
- Timestamp authority (TSA) support
- Rate-limited to 30 ops/min

```csharp
public interface IDigitalSigningService
{
    Task<SignResult> SignPdfAsync(Stream pdfStream, SigningOptions options);
    Task<bool> VerifySignatureAsync(Stream signedPdf);
}
```

### 9.3 Biometric Matching

- DigitalPersona SDK (Windows only, DLL-based)
- Falls back to `StubBiometricMatcherService` on non-Windows
- Real-time matching feedback via FingerprintHub

```csharp
public interface IBiometricMatcher
{
    Task<MatchResult> MatchAsync(byte[] template, CancellationToken ct);
    Task EnrollAsync(Guid patientId, byte[] template, CancellationToken ct);
}
```

### 9.4 Backup & Disaster Recovery

| Service                   | Purpose                                  |
| ------------------------- | ---------------------------------------- |
| `DatabaseBackupService`   | PostgreSQL `pg_dump` to disk             |
| `MinioBackupService`      | MinIO bucket snapshots                   |
| `WalBackupService`        | WAL archiving for point-in-time recovery |
| `CloudReplicationService` | Sync DB + MinIO to remote server         |
| `BackupSchedulerService`  | Cron-based scheduling                    |
| `BackupComplianceService` | 3-2-1 strategy compliance audit          |

### 9.5 Certificate Authority (Private PKI)

Full private PKI for TLS certificate management. See [docs/ca_mtls_deployment.md](ca_mtls_deployment.md) for comprehensive details.

### 9.6 Background Services

Auto-started hosted services:

```csharp
builder.Services.AddHostedService<PartitionMaintenanceService>();   // Audit log partition creation
builder.Services.AddHostedService<DataBackupSchedulerService>();     // Scheduled backups
builder.Services.AddHostedService<CloudReplicationSchedulerService>();// DB + MinIO replication
builder.Services.AddHostedService<CertAutoRenewalService>();         // Certificate auto-renewal
builder.Services.AddHostedService<BiometricMatcherService>();        // Fingerprint engine (Windows)
```

### 9.7 Audit Logging

Automatic via `AuditInterceptor` on `DbContext.SaveChanges()`:

- Captures entity changes (Create, Update, Delete)
- Records user ID from JWT claims
- Stores `changes` as JSONB diff
- Partitioned table by month with automatic future partition creation

---

## 10. Database & Migrations

### Connection

```
Host=localhost;Port=5433;Database=ivf_db;Username=postgres;Password=postgres;SSL Mode=Require;Trust Server Certificate=true
```

### DbContext

`IvfDbContext` has 60+ `DbSet<>` properties. Entity configurations are in separate `IEntityTypeConfiguration<T>` classes.

### Creating Migrations

```bash
# Add migration
dotnet ef migrations add <MigrationName> \
  --project src/IVF.Infrastructure \
  --startup-project src/IVF.API

# Apply migration
dotnet ef database update \
  --project src/IVF.Infrastructure \
  --startup-project src/IVF.API

# Remove last migration (if not applied)
dotnet ef migrations remove \
  --project src/IVF.Infrastructure \
  --startup-project src/IVF.API
```

### Seeding

On startup in development, these seeders run automatically via `DatabaseSeeder.SeedAsync()`:

| Seeder                       | Purpose                                |
| ---------------------------- | -------------------------------------- |
| `DatabaseSeeder`             | Orchestrator — calls all other seeders |
| `FlowSeeder`                 | Default workflow configurations        |
| `FormTemplateSeeder`         | Built-in clinical form templates       |
| `ConceptSeeder`              | Medical concept library                |
| `MenuSeeder`                 | Navigation menu items                  |
| `PermissionDefinitionSeeder` | Default role-permission mappings       |

### Key Tables

| Schema/Table     | Entity         | Description                     |
| ---------------- | -------------- | ------------------------------- |
| patients         | Patient        | Core patient records            |
| couples          | Couple         | Wife + husband linkage          |
| treatment_cycles | TreatmentCycle | IVF/IUI/ICSI treatment tracking |
| queue_tickets    | QueueTicket    | Reception queue                 |
| embryos          | Embryo         | Embryo culture & grading        |
| form_templates   | FormTemplate   | Dynamic form definitions        |
| form_responses   | FormResponse   | Submitted form data             |
| invoices         | Invoice        | Billing                         |
| audit_logs       | AuditLog       | Partitioned audit trail         |
| users            | User           | System users with roles         |

---

## 11. Testing

### Test Project

```
tests/IVF.Tests/
├── Application/
│   └── PatientCommandsTests.cs   ← CQRS handler tests
├── Domain/                        ← Entity logic tests
└── IVF.Tests.csproj
```

### Running Tests

```bash
dotnet test tests/IVF.Tests/IVF.Tests.csproj
dotnet test --filter "FullyQualifiedName~PatientCommands"
```

### Frontend Tests

```bash
cd ivf-client
npm test       # Vitest
```

### Writing a Test

```csharp
public class CreatePatientHandlerTests
{
    private readonly Mock<IPatientRepository> _repoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();

    [Fact]
    public async Task Handle_ValidCommand_ReturnsSuccess()
    {
        // Arrange
        _repoMock.Setup(r => r.GenerateCodeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("P-000001");

        var handler = new CreatePatientHandler(_repoMock.Object, _uowMock.Object);
        var command = new CreatePatientCommand("Nguyễn Văn A", DateTime.Today.AddYears(-30),
            Gender.Male, PatientType.Infertility, null, null);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("P-000001", result.Value!.PatientCode);
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

---

## 12. Docker & Deployment

### Docker Compose Services

```bash
docker-compose up -d    # Start all services
docker-compose ps       # Check status
docker-compose logs -f  # Follow logs
```

| Service            | Container           | Ports      | Network                         |
| ------------------ | ------------------- | ---------- | ------------------------------- |
| PostgreSQL Primary | `ivf-db`            | 5433:5432  | ivf-data                        |
| PostgreSQL Standby | `ivf-db-standby`    | 5434:5432  | ivf-data (profile: replication) |
| Redis              | `ivf-redis`         | 6379       | ivf-data                        |
| MinIO              | `ivf-minio`         | 9000, 9001 | ivf-data, ivf-public            |
| EJBCA              | `ivf-ejbca`         | 8443, 8442 | ivf-signing, ivf-data           |
| SignServer         | `ivf-signserver`    | 9443       | ivf-signing, ivf-data           |
| EJBCA DB           | `ivf-ejbca-db`      | —          | ivf-data                        |
| SignServer DB      | `ivf-signserver-db` | —          | ivf-data                        |

### Network Segmentation

```
ivf-public    — API, frontend access (bridge, internet-facing)
ivf-signing   — API ↔ SignServer ↔ EJBCA (internal, no internet)
ivf-data      — Databases, Redis, MinIO (internal, no internet)
```

### Remote Replica

A remote server at `172.16.102.11` hosts replicas:

- `ivf-db-replica` (port 5201) — PostgreSQL streaming replication
- `ivf-minio-replica` (port 9000) — MinIO bucket mirroring

Managed via SSH/SCP from the API server.

---

## 13. Configuration Reference

### appsettings.json Structure

```jsonc
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=...;Port=5433;Database=ivf_db;...",
    "Redis": "localhost:6379,abortConnect=false",
  },
  "JwtSettings": {
    "Secret": "...",
    "Issuer": "IVF-System",
    "Audience": "IVF-Users",
    "ExpiryMinutes": 60,
    "RefreshTokenExpiryDays": 7,
  },
  "MinIO": {
    "Endpoint": "localhost:9000",
    "AccessKey": "minioadmin",
    "SecretKey": "minioadmin123",
    "UseSSL": true,
    "DocumentsBucket": "ivf-documents",
    "SignedPdfsBucket": "ivf-signed-pdfs",
    "MedicalImagesBucket": "ivf-medical-images",
  },
  "DigitalSigning": {
    "Enabled": true,
    "SignServerUrl": "https://localhost:9443/signserver",
    "WorkerName": "PDFSigner",
    "ClientCertificatePath": "...",
    "SkipTlsValidation": true,
    "SigningRateLimitPerMinute": 30,
  },
  "CertificateAuthority": {
    "BaseUrl": "http://localhost:5000",
  },
  "Matcher": {
    "ShardId": 0,
    "TotalShards": 1,
  },
}
```

### Angular Environments

```typescript
// src/environments/environment.ts
export const environment = {
  production: false,
  apiUrl: "http://localhost:5000/api",
};
```

---

## 14. Coding Conventions

### Backend (.NET)

| Convention                     | Example                                                                  |
| ------------------------------ | ------------------------------------------------------------------------ |
| Entity factory methods         | `Patient.Create(code, name, ...)`                                        |
| Record types for DTOs          | `public record PatientDto(Guid Id, string Name, ...)`                    |
| Record types for CQRS          | `public record CreatePatientCommand(...) : IRequest<Result<PatientDto>>` |
| Primary constructors for DI    | `public class Handler(IRepo repo) : IRequestHandler<...>`                |
| `AsNoTracking()` for reads     | Always in query repositories                                             |
| Result pattern (no exceptions) | `Result<T>.Success(...)` / `Result<T>.Failure(...)`                      |
| FluentValidation validators    | Same file as command, class named `{Command}Validator`                   |
| No SaveChanges in repos        | Use `IUnitOfWork.SaveChangesAsync()` in handlers                         |
| Cancellation token always      | Pass through all async methods                                           |

### Frontend (Angular)

| Convention                       | Example                                                                    |
| -------------------------------- | -------------------------------------------------------------------------- |
| Standalone components            | `standalone: true` (no NgModules)                                          |
| Signals for state                | `patients = signal<Patient[]>([])`                                         |
| Lazy loading                     | `loadComponent: () => import(...)`                                         |
| Injectable root services         | `@Injectable({ providedIn: 'root' })`                                      |
| Functional guards                | `export const authGuard: CanActivateFn = () => { ... }`                    |
| Functional interceptors          | `export const authInterceptor: HttpInterceptorFn = (req, next) => { ... }` |
| RxJS for async                   | `Observable<T>`, `subscribe({ next, error })`                              |
| TypeScript interfaces for models | `export interface Patient { ... }`                                         |

### API Response Patterns

```
GET    /api/resource           → 200 { items: [...], totalCount, page, pageSize }
GET    /api/resource/{id}      → 200 { ... } or 404
POST   /api/resource           → 201 { id, ... } or 400 { errors: [...] }
PUT    /api/resource/{id}      → 200 { ... } or 404
DELETE /api/resource/{id}      → 204 or 404
```

---

## 15. Adding a New Feature (Step-by-Step)

Here's how to add a new feature end-to-end, using "Appointments" as an example.

### Step 1: Domain Entity

```csharp
// src/IVF.Domain/Entities/Appointment.cs
public class Appointment : BaseEntity
{
    public Guid PatientId { get; private set; }
    public Guid DoctorId { get; private set; }
    public DateTime ScheduledAt { get; private set; }
    public string? Notes { get; private set; }
    public AppointmentStatus Status { get; private set; }

    public static Appointment Create(Guid patientId, Guid doctorId, DateTime scheduledAt, string? notes)
    {
        return new Appointment
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            DoctorId = doctorId,
            ScheduledAt = scheduledAt,
            Notes = notes,
            Status = AppointmentStatus.Scheduled,
        };
    }
}
```

### Step 2: Repository Interface

```csharp
// src/IVF.Application/Common/Interfaces/IAppointmentRepository.cs
public interface IAppointmentRepository
{
    Task<Appointment?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Appointment>> GetByPatientAsync(Guid patientId, CancellationToken ct = default);
    Task AddAsync(Appointment appt, CancellationToken ct = default);
    Task UpdateAsync(Appointment appt, CancellationToken ct = default);
}
```

### Step 3: CQRS Commands & Queries

```csharp
// src/IVF.Application/Features/Appointments/Commands/AppointmentCommands.cs
public record CreateAppointmentCommand(
    Guid PatientId, Guid DoctorId, DateTime ScheduledAt, string? Notes
) : IRequest<Result<AppointmentDto>>;

public class CreateAppointmentValidator : AbstractValidator<CreateAppointmentCommand>
{
    public CreateAppointmentValidator()
    {
        RuleFor(x => x.PatientId).NotEmpty();
        RuleFor(x => x.DoctorId).NotEmpty();
        RuleFor(x => x.ScheduledAt).GreaterThan(DateTime.UtcNow);
    }
}

public class CreateAppointmentHandler(
    IAppointmentRepository repo, IUnitOfWork uow
) : IRequestHandler<CreateAppointmentCommand, Result<AppointmentDto>>
{
    public async Task<Result<AppointmentDto>> Handle(
        CreateAppointmentCommand request, CancellationToken ct)
    {
        var appt = Appointment.Create(request.PatientId, request.DoctorId,
            request.ScheduledAt, request.Notes);
        await repo.AddAsync(appt, ct);
        await uow.SaveChangesAsync(ct);
        return Result<AppointmentDto>.Success(AppointmentDto.FromEntity(appt));
    }
}
```

### Step 4: EF Configuration + Repository

```csharp
// src/IVF.Infrastructure/Persistence/Configurations/AppointmentConfiguration.cs
public class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.ScheduledAt).IsRequired();
        builder.HasIndex(a => a.PatientId);
        builder.HasIndex(a => a.ScheduledAt);
    }
}

// src/IVF.Infrastructure/Repositories/AppointmentRepository.cs
public class AppointmentRepository(IvfDbContext ctx) : IAppointmentRepository { ... }
```

### Step 5: Register in DI

```csharp
// src/IVF.Infrastructure/DependencyInjection.cs
services.AddScoped<IAppointmentRepository, AppointmentRepository>();
```

### Step 6: Add to DbContext

```csharp
// src/IVF.Infrastructure/Persistence/IvfDbContext.cs
public DbSet<Appointment> Appointments => Set<Appointment>();
```

### Step 7: Create Migration

```bash
dotnet ef migrations add AddAppointments \
  --project src/IVF.Infrastructure \
  --startup-project src/IVF.API
```

### Step 8: API Endpoint

```csharp
// src/IVF.API/Endpoints/AppointmentEndpoints.cs
public static class AppointmentEndpoints
{
    public static void MapAppointmentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/appointments")
            .WithTags("Appointments")
            .RequireAuthorization();

        group.MapPost("/", async (CreateAppointmentCommand cmd, IMediator m) =>
        {
            var r = await m.Send(cmd);
            return r.IsSuccess
                ? Results.Created($"/api/appointments/{r.Value!.Id}", r.Value)
                : Results.BadRequest(r.Error);
        });
    }
}

// Register in Program.cs:
app.MapAppointmentEndpoints();
```

### Step 9: Angular Model + Service + Component

```typescript
// core/models/appointment.models.ts
export interface Appointment {
  id: string;
  patientId: string;
  doctorId: string;
  scheduledAt: string;
  notes?: string;
  status: string;
}

// core/services/appointment.service.ts
@Injectable({ providedIn: 'root' })
export class AppointmentService {
  private readonly url = `${environment.apiUrl}/appointments`;
  constructor(private http: HttpClient) {}
  create(req: CreateAppointmentRequest): Observable<Appointment> {
    return this.http.post<Appointment>(this.url, req);
  }
}

// features/appointments/appointment-list.component.ts
@Component({ selector: 'app-appointment-list', standalone: true, ... })
export class AppointmentListComponent { ... }
```

### Step 10: Add Route

```typescript
// app.routes.ts — inside the children array
{
  path: 'appointments',
  loadComponent: () => import('./features/appointments/appointment-list/appointment-list.component')
    .then(m => m.AppointmentListComponent),
},
```

---

## 16. Troubleshooting

### Common Issues

**Port 5000 already in use:**

```powershell
Get-NetTCPConnection -LocalPort 5000 -State Listen |
  ForEach-Object { Stop-Process -Id $_.OwningProcess -Force }
```

**PostgreSQL connection refused:**

```bash
docker-compose ps    # Check if ivf-db is running
docker-compose logs db  # Check for errors
```

**Migration failures:**

```bash
# Remove failed migration
dotnet ef migrations remove --project src/IVF.Infrastructure --startup-project src/IVF.API

# Re-create from clean state
dotnet ef database update --project src/IVF.Infrastructure --startup-project src/IVF.API
```

**Angular build errors (CSS budget):**
Check `angular.json` budget settings if CSS grows past limits.

**Redis unavailable:**
Redis degrades gracefully — the app works without it but without caching.

**MinIO self-signed cert errors:**
The API uses `ServerCertificateCustomValidationCallback` to trust the private CA.

### Logs

```bash
# Backend logs (console output or structured logging)
dotnet run --project src/IVF.API/IVF.API.csproj

# Docker container logs
docker logs ivf-db -f
docker logs ivf-minio -f
docker logs ivf-signserver -f

# Check PostgreSQL SSL
docker exec ivf-db psql -U postgres -c "SHOW ssl;"
```

---

## Related Documents

| Document              | Path                                                                    | Description                                 |
| --------------------- | ----------------------------------------------------------------------- | ------------------------------------------- |
| CA & Deployment       | [docs/ca_mtls_deployment.md](ca_mtls_deployment.md)                     | Certificate Authority, mTLS, TLS deployment |
| Form Builder          | [docs/form_report_builder.md](form_report_builder.md)                   | Dynamic form/report system architecture     |
| Digital Signing       | [docs/digital_signing.md](digital_signing.md)                           | SignServer + EJBCA signing infrastructure   |
| Biometrics            | [docs/matcher_infrastructure_guide.md](matcher_infrastructure_guide.md) | DigitalPersona fingerprint setup            |
| Backup & Restore      | [docs/backup_and_restore.md](backup_and_restore.md)                     | Backup strategy and procedures              |
| Database Optimization | [docs/database_optimization_report.md](database_optimization_report.md) | Query performance tuning                    |
