---
description: "Use when writing or modifying Minimal API endpoints, error responses, authorization policies, and OpenAPI metadata. Enforces REST conventions, Result<T> response wrapping, and endpoint registration standards for the IVF .NET 10 project."
applyTo: "src/IVF.API/**"
---

# API Design Conventions

## Endpoint Structure

File: `src/IVF.API/Endpoints/{Feature}Endpoints.cs`

```csharp
public static class PatientEndpoints
{
    public static void MapPatientEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/patients")
            .WithTags("Patients")
            .RequireAuthorization();

        group.MapGet("/", async (
            [AsParameters] SearchPatientsQuery query,
            IMediator mediator) =>
        {
            var result = await mediator.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        });

        group.MapGet("/{id:guid}", async (Guid id, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetPatientByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        });

        group.MapPost("/", async (CreatePatientCommand command, IMediator mediator) =>
        {
            var result = await mediator.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/patients/{result.Value!.Id}", result.Value)
                : Results.BadRequest(result.Error);
        });

        group.MapPut("/{id:guid}", async (Guid id, UpdatePatientCommand command, IMediator mediator) =>
        {
            var result = await mediator.Send(command with { Id = id });
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        });

        group.MapDelete("/{id:guid}", async (Guid id, IMediator mediator) =>
        {
            var result = await mediator.Send(new DeletePatientCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        });
    }
}
```

## Key Rules

### Routing

1. **Base path**: `/api/{feature}` — lowercase, plural (e.g., `/api/patients`, `/api/cycles`)
2. **Resource ID**: `/{id:guid}` — always typed as `Guid`
3. **Sub-resources**: `/api/patients/{patientId:guid}/cycles` — nested under parent
4. **Admin routes**: `/api/admin/{feature}` — for administrative operations
5. **No verb-based routes** — use HTTP methods, not `/api/patients/search`

### HTTP Methods

| Method         | Purpose     | Return           | Status Code         |
| -------------- | ----------- | ---------------- | ------------------- |
| `GET /`        | List/search | `PagedResult<T>` | 200 OK              |
| `GET /{id}`    | Get by ID   | `Result<T>`      | 200 OK / 404        |
| `POST /`       | Create      | `Result<T>`      | 201 Created / 400   |
| `PUT /{id}`    | Full update | `Result<T>`      | 200 OK / 404        |
| `DELETE /{id}` | Soft delete | `Result`         | 204 NoContent / 404 |

### Response Mapping

```csharp
// Success patterns
Results.Ok(result.Value)                                          // 200 — GET, PUT
Results.Created($"/api/{feature}/{result.Value!.Id}", result.Value) // 201 — POST
Results.NoContent()                                               // 204 — DELETE

// Error patterns
Results.BadRequest(result.Error)    // 400 — validation failure
Results.NotFound(result.Error)      // 404 — entity not found
Results.Forbid()                    // 403 — insufficient permissions
```

### Endpoint Group Setup

Every endpoint file MUST have:

```csharp
var group = app.MapGroup("/api/{feature}")
    .WithTags("{Feature}")           // OpenAPI/Swagger grouping
    .RequireAuthorization();         // JWT auth required (default)
```

**Authorization policies** (use when stricter access needed):

```csharp
.RequireAuthorization("AdminOnly")       // Admin role only
.RequireAuthorization("MedicalStaff")    // Doctor, Nurse, LabTech, Embryologist
.RequireAuthorization("DoctorOrAdmin")   // Doctor or Admin
.RequireAuthorization("LabAccess")       // LabTech, Embryologist, Doctor
.RequireAuthorization("BillingAccess")   // Cashier, Admin
.RequireAuthorization("QueueManagement") // Receptionist, Nurse, Admin
```

### Registration

Every endpoint group must be registered in `Program.cs`:

```csharp
app.MapPatientEndpoints();
app.MapCycleEndpoints();
// ... (60+ endpoint groups registered)
```

## MediatR Integration

- **No business logic in endpoints** — endpoints only dispatch to MediatR
- **Inject `IMediator` per-handler** via lambda parameters (not constructor)
- **Query parameters**: use `[AsParameters]` for GET query binding
- **Body parameters**: bind directly from request body for POST/PUT

```csharp
// Query parameters (GET)
group.MapGet("/", async ([AsParameters] SearchQuery query, IMediator mediator) => ...);

// Body parameters (POST/PUT)
group.MapPost("/", async (CreateCommand command, IMediator mediator) => ...);

// Route + body (PUT with ID from route)
group.MapPut("/{id:guid}", async (Guid id, UpdateCommand command, IMediator mediator) =>
{
    var result = await mediator.Send(command with { Id = id });
    ...
});
```

## Error Handling

Exception middleware in `Program.cs` automatically maps:

| Exception                      | HTTP Status | Response                           |
| ------------------------------ | ----------- | ---------------------------------- |
| `ValidationException`          | 400         | `{ errors: [...] }`                |
| `TenantLimitExceededException` | 403         | `{ message, limit, feature }`      |
| `FeatureNotEnabledException`   | 403         | `{ message, featureCode }`         |
| Unhandled exception            | 500         | `{ message: "An error occurred" }` |

DO NOT add `try/catch` in endpoints — let the middleware handle it.

## Pagination

All list endpoints MUST support pagination:

```csharp
// Query record
public record SearchPatientsQuery(
    string? Q = null,
    int Page = 1,
    int PageSize = 20
) : IRequest<Result<PagedResult<PatientDto>>>;

// Response shape
{
    "items": [...],
    "totalCount": 150,
    "page": 1,
    "pageSize": 20,
    "totalPages": 8
}
```

## OpenAPI / Swagger

- Available at `/swagger` in development only
- `WithTags()` groups endpoints in Swagger UI
- Use descriptive parameter names for documentation
- No explicit `[ProducesResponseType]` needed — Minimal API infers from `Results.*`

## DO NOTs

- **No controllers** — Minimal API only (no `[ApiController]`, no `ControllerBase`)
- **No `IActionResult`** — use `Results.Ok()`, `Results.NotFound()`, etc.
- **No business logic** in endpoints — delegate everything to MediatR handlers
- **No direct DbContext** in endpoints — always go through MediatR → handler → repository
- **No `try/catch`** in endpoints — exception middleware handles all errors
- **No hardcoded strings** for auth policies — use the defined policy names
