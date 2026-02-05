---
name: dotnet-enterprise
description: Enterprise .NET patterns following Microsoft/Google engineering standards. Implements Clean Architecture, CQRS, DDD principles, Minimal APIs, comprehensive testing, and production-ready observability. Use for building scalable, maintainable backend services.
version: 1.0
---

# .NET Enterprise Architecture

Enterprise-grade .NET patterns based on Microsoft architecture guidance and large-scale service best practices.

## When to Use

- Building new APIs or microservices
- Implementing domain logic with DDD patterns
- Designing data access and persistence layers
- Setting up testing, security, or observability

## Core Principles

1. **Clean Architecture** - Dependency inversion, domain at center
2. **CQRS for Complexity** - Separate read/write models when beneficial
3. **Minimal APIs** - Prefer over controllers for new endpoints
4. **Domain-Driven Design** - Rich domain models, value objects
5. **Vertical Slices** - Feature folders over layer folders

## Project Structure

```
src/
├── Domain/                      # Enterprise business rules
│   ├── Entities/               # Aggregate roots and entities
│   ├── ValueObjects/           # Immutable value types
│   ├── Events/                 # Domain events
│   ├── Exceptions/             # Domain exceptions
│   └── Interfaces/             # Repository interfaces
├── Application/                 # Application business rules
│   ├── Common/                 # Shared behaviors, interfaces
│   │   ├── Behaviors/          # MediatR pipeline behaviors
│   │   └── Interfaces/         # Application service interfaces
│   └── Features/               # Feature slices (CQRS)
│       └── [Feature]/
│           ├── Commands/       # Write operations
│           ├── Queries/        # Read operations
│           └── EventHandlers/  # Domain event handlers
├── Infrastructure/              # External concerns
│   ├── Persistence/            # EF Core, repositories
│   ├── Services/               # External service clients
│   └── DependencyInjection.cs  # Service registration
└── API/                         # Presentation layer
    ├── Endpoints/              # Minimal API endpoints
    ├── Middleware/             # Custom middleware
    └── Program.cs              # Host configuration
```

## Domain Patterns

### Entity with Domain Events

```csharp
public abstract class BaseEntity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();
    
    private readonly List<IDomainEvent> _domainEvents = [];
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void AddDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    public void ClearDomainEvents() => _domainEvents.Clear();
}

public class Patient : BaseEntity
{
    public string FullName { get; private set; } = string.Empty;
    public DateOnly DateOfBirth { get; private set; }
    public Email Email { get; private set; } = null!;
    public PatientStatus Status { get; private set; } = PatientStatus.Active;

    private Patient() { } // EF Core

    public static Patient Create(string fullName, DateOnly dateOfBirth, string email)
    {
        var patient = new Patient
        {
            FullName = Guard.Against.NullOrWhiteSpace(fullName),
            DateOfBirth = dateOfBirth,
            Email = Email.Create(email),
        };
        patient.AddDomainEvent(new PatientCreatedEvent(patient.Id));
        return patient;
    }

    public void UpdateContactInfo(string email)
    {
        Email = Email.Create(email);
        AddDomainEvent(new PatientUpdatedEvent(Id));
    }

    public void Deactivate()
    {
        if (Status == PatientStatus.Inactive)
            throw new DomainException("Patient is already inactive");
        
        Status = PatientStatus.Inactive;
        AddDomainEvent(new PatientDeactivatedEvent(Id));
    }
}
```

### Value Object

```csharp
public sealed record Email
{
    public string Value { get; }

    private Email(string value) => Value = value;

    public static Email Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("Email cannot be empty");
        
        if (!value.Contains('@'))
            throw new DomainException("Invalid email format");

        return new Email(value.ToLowerInvariant().Trim());
    }

    public static implicit operator string(Email email) => email.Value;
}
```

## CQRS Patterns

### Command with Validation

```csharp
public sealed record CreatePatientCommand(
    string FullName,
    DateOnly DateOfBirth,
    string Email
) : IRequest<Result<Guid>>;

public sealed class CreatePatientCommandValidator 
    : AbstractValidator<CreatePatientCommand>
{
    public CreatePatientCommandValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.DateOfBirth)
            .LessThan(DateOnly.FromDateTime(DateTime.Today))
            .WithMessage("Date of birth must be in the past");

        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();
    }
}

public sealed class CreatePatientCommandHandler(
    IPatientRepository repository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<CreatePatientCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreatePatientCommand request, 
        CancellationToken cancellationToken)
    {
        var patient = Patient.Create(
            request.FullName,
            request.DateOfBirth,
            request.Email);

        await repository.AddAsync(patient, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Ok(patient.Id);
    }
}
```

### Query with Projection

```csharp
public sealed record GetPatientsQuery(
    string? SearchTerm,
    int Page = 1,
    int PageSize = 20
) : IRequest<PagedResult<PatientDto>>;

public sealed class GetPatientsQueryHandler(
    IApplicationDbContext context)
    : IRequestHandler<GetPatientsQuery, PagedResult<PatientDto>>
{
    public async Task<PagedResult<PatientDto>> Handle(
        GetPatientsQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.Patients.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.ToLower();
            query = query.Where(p => 
                p.FullName.ToLower().Contains(term) ||
                p.Email.Value.Contains(term));
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(p => p.FullName)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(p => new PatientDto(
                p.Id,
                p.FullName,
                p.DateOfBirth,
                p.Email.Value,
                p.Status.ToString()))
            .ToListAsync(cancellationToken);

        return new PagedResult<PatientDto>(items, total, request.Page, request.PageSize);
    }
}
```

## Minimal API Endpoints

### Endpoint Pattern

```csharp
public static class PatientEndpoints
{
    public static void MapPatientEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/patients")
            .WithTags("Patients")
            .RequireAuthorization();

        group.MapGet("/", GetPatients)
            .WithName("GetPatients")
            .Produces<PagedResult<PatientDto>>();

        group.MapGet("/{id:guid}", GetPatient)
            .WithName("GetPatient")
            .Produces<PatientDto>()
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/", CreatePatient)
            .WithName("CreatePatient")
            .Produces<Guid>(StatusCodes.Status201Created)
            .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest);

        group.MapPut("/{id:guid}", UpdatePatient)
            .WithName("UpdatePatient")
            .Produces(StatusCodes.Status204NoContent);

        group.MapDelete("/{id:guid}", DeletePatient)
            .WithName("DeletePatient")
            .Produces(StatusCodes.Status204NoContent)
            .RequireAuthorization("AdminOnly");
    }

    private static async Task<IResult> GetPatients(
        [AsParameters] GetPatientsQuery query,
        ISender sender,
        CancellationToken ct)
    {
        var result = await sender.Send(query, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetPatient(
        Guid id,
        ISender sender,
        CancellationToken ct)
    {
        var result = await sender.Send(new GetPatientQuery(id), ct);
        return result.IsSuccess 
            ? Results.Ok(result.Value) 
            : Results.NotFound();
    }

    private static async Task<IResult> CreatePatient(
        CreatePatientCommand command,
        ISender sender,
        CancellationToken ct)
    {
        var result = await sender.Send(command, ct);
        return result.IsSuccess
            ? Results.CreatedAtRoute("GetPatient", new { id = result.Value }, result.Value)
            : Results.BadRequest(result.Errors);
    }
}
```

## Repository Pattern

```csharp
public interface IRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(T entity, CancellationToken ct = default);
    void Update(T entity);
    void Remove(T entity);
}

public class Repository<T>(IApplicationDbContext context) 
    : IRepository<T> where T : BaseEntity
{
    protected readonly DbSet<T> DbSet = context.Set<T>();

    public virtual async Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await DbSet.FindAsync([id], ct);

    public virtual async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default)
        => await DbSet.ToListAsync(ct);

    public virtual async Task AddAsync(T entity, CancellationToken ct = default)
        => await DbSet.AddAsync(entity, ct);

    public virtual void Update(T entity)
        => DbSet.Update(entity);

    public virtual void Remove(T entity)
        => DbSet.Remove(entity);
}
```

## Testing Patterns

### Integration Test with TestContainers

```csharp
public class PatientEndpointsTests : IClassFixture<ApiFixture>
{
    private readonly HttpClient _client;
    private readonly ApiFixture _fixture;

    public PatientEndpointsTests(ApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.CreateClient();
    }

    [Fact]
    public async Task CreatePatient_WithValidData_ReturnsCreated()
    {
        // Arrange
        var command = new CreatePatientCommand(
            "John Doe",
            new DateOnly(1990, 1, 1),
            "john@example.com");

        // Act
        var response = await _client.PostAsJsonAsync("/api/patients", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = await response.Content.ReadFromJsonAsync<Guid>();
        id.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CreatePatient_WithInvalidEmail_ReturnsBadRequest()
    {
        // Arrange
        var command = new CreatePatientCommand("John Doe", new DateOnly(1990, 1, 1), "invalid");

        // Act
        var response = await _client.PostAsJsonAsync("/api/patients", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}

public class ApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(_postgres.GetConnectionString()));
        });
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
    }

    public new async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
    }
}
```

## Observability

### OpenTelemetry Setup

```csharp
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(serviceName: "MyApi", serviceVersion: "1.0.0"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        .AddOtlpExporter())
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddOtlpExporter());

builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>()
    .AddRedis(builder.Configuration.GetConnectionString("Redis")!);
```

## Security

### Authorization Policies

```csharp
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"))
    .AddPolicy("CanManagePatients", policy => policy
        .RequireAuthenticatedUser()
        .RequireClaim("permission", "patients:write"));
```

### Validation Pipeline Behavior

```csharp
public sealed class ValidationBehavior<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!validators.Any()) return await next();

        var context = new ValidationContext<TRequest>(request);
        var failures = validators
            .Select(v => v.Validate(context))
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count != 0)
            throw new ValidationException(failures);

        return await next();
    }
}
```

## Code Style

- **Naming**: PascalCase for public, _camelCase for private fields
- **Primary Constructors**: Prefer for DI
- **Records**: Use for DTOs and commands/queries
- **File-scoped namespaces**: Always
- **Nullable reference types**: Enabled
- **Max file length**: 300 lines
- **Max method length**: 30 lines

See `resources/` for detailed patterns:
- `resources/clean-architecture.md` - Layer responsibilities
- `resources/cqrs-patterns.md` - Advanced CQRS scenarios
- `resources/testing.md` - Comprehensive testing strategies
