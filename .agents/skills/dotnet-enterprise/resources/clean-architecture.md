# .NET Clean Architecture

Layer responsibilities and dependency rules for enterprise applications.

## Layer Overview

```
┌─────────────────────────────────────────┐
│              Presentation               │  ← API/UI (depends on Application)
├─────────────────────────────────────────┤
│              Application                │  ← Use cases (depends on Domain)
├─────────────────────────────────────────┤
│             Infrastructure              │  ← External concerns (depends on Application)
├─────────────────────────────────────────┤
│                 Domain                  │  ← Core business (depends on nothing)
└─────────────────────────────────────────┘
```

## Domain Layer

**Responsibilities:**
- Enterprise business rules
- Entities and aggregate roots
- Value objects
- Domain events
- Domain exceptions
- Repository interfaces (contracts only)

**Dependencies:** None (innermost layer)

```csharp
// Domain/Entities/TreatmentCycle.cs
public class TreatmentCycle : AggregateRoot
{
    public Guid PatientId { get; private set; }
    public CycleType Type { get; private set; }
    public CyclePhase CurrentPhase { get; private set; }
    public DateOnly StartDate { get; private set; }
    
    private readonly List<CycleEvent> _events = [];
    public IReadOnlyCollection<CycleEvent> Events => _events.AsReadOnly();

    public void AdvanceToPhase(CyclePhase newPhase)
    {
        if (!CanAdvanceTo(newPhase))
            throw new DomainException($"Cannot advance from {CurrentPhase} to {newPhase}");

        var previousPhase = CurrentPhase;
        CurrentPhase = newPhase;
        _events.Add(new CycleEvent(DateTime.UtcNow, $"Phase changed: {previousPhase} → {newPhase}"));
        AddDomainEvent(new CyclePhaseChangedEvent(Id, previousPhase, newPhase));
    }

    private bool CanAdvanceTo(CyclePhase newPhase) => (CurrentPhase, newPhase) switch
    {
        (CyclePhase.Stimulation, CyclePhase.Trigger) => true,
        (CyclePhase.Trigger, CyclePhase.Retrieval) => true,
        (CyclePhase.Retrieval, CyclePhase.Culture) => true,
        (CyclePhase.Culture, CyclePhase.Transfer) => true,
        (CyclePhase.Transfer, CyclePhase.Luteal) => true,
        _ => false,
    };
}
```

## Application Layer

**Responsibilities:**
- Application business rules (use cases)
- CQRS commands and queries
- DTOs and mapping
- Validation
- Application service interfaces
- External service interfaces (defined here, implemented in Infrastructure)

**Dependencies:** Domain only

```csharp
// Application/Features/Cycles/Commands/AdvanceCyclePhase.cs
public sealed record AdvanceCyclePhaseCommand(Guid CycleId, CyclePhase NewPhase) 
    : IRequest<Result>;

public sealed class AdvanceCyclePhaseHandler(
    ICycleRepository repository,
    IUnitOfWork unitOfWork,
    ILogger<AdvanceCyclePhaseHandler> logger)
    : IRequestHandler<AdvanceCyclePhaseCommand, Result>
{
    public async Task<Result> Handle(
        AdvanceCyclePhaseCommand request, 
        CancellationToken ct)
    {
        var cycle = await repository.GetByIdAsync(request.CycleId, ct);
        if (cycle is null)
            return Result.NotFound($"Cycle {request.CycleId} not found");

        try
        {
            cycle.AdvanceToPhase(request.NewPhase);
            await unitOfWork.SaveChangesAsync(ct);
            logger.LogInformation("Cycle {CycleId} advanced to {Phase}", request.CycleId, request.NewPhase);
            return Result.Ok();
        }
        catch (DomainException ex)
        {
            return Result.Invalid(ex.Message);
        }
    }
}
```

## Infrastructure Layer

**Responsibilities:**
- Database (EF Core DbContext, repositories)
- External services (HTTP clients, file storage)
- Identity/auth implementation
- Caching
- Messaging

**Dependencies:** Application (implements its interfaces)

```csharp
// Infrastructure/Persistence/Repositories/CycleRepository.cs
public sealed class CycleRepository(AppDbContext context) : ICycleRepository
{
    public async Task<TreatmentCycle?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        return await context.TreatmentCycles
            .Include(c => c.Events)
            .FirstOrDefaultAsync(c => c.Id == id, ct);
    }

    public async Task<IReadOnlyList<TreatmentCycle>> GetByPatientAsync(Guid patientId, CancellationToken ct)
    {
        return await context.TreatmentCycles
            .Where(c => c.PatientId == patientId)
            .OrderByDescending(c => c.StartDate)
            .ToListAsync(ct);
    }

    public async Task AddAsync(TreatmentCycle cycle, CancellationToken ct)
    {
        await context.TreatmentCycles.AddAsync(cycle, ct);
    }
}
```

## API Layer

**Responsibilities:**
- HTTP endpoints (Minimal APIs or Controllers)
- Request/response mapping
- Authorization policies
- Middleware
- OpenAPI documentation

**Dependencies:** Application

```csharp
// API/Endpoints/CycleEndpoints.cs
public static class CycleEndpoints
{
    public static void MapCycleEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/cycles")
            .WithTags("Treatment Cycles")
            .RequireAuthorization();

        group.MapPost("/{id:guid}/advance", async (
            Guid id,
            AdvancePhaseRequest request,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(
                new AdvanceCyclePhaseCommand(id, request.NewPhase), ct);
            
            return result.IsSuccess ? Results.NoContent() : result.ToResult();
        })
        .WithName("AdvanceCyclePhase")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status400BadRequest);
    }
}
```

## Dependency Injection Setup

```csharp
// Infrastructure/DependencyInjection.cs
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Database")));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ICycleRepository, CycleRepository>();
        services.AddScoped<IPatientRepository, PatientRepository>();

        return services;
    }
}

// Application/DependencyInjection.cs
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg => {
            cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly);
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        });

        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        return services;
    }
}
```
