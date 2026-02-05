# CQRS Patterns for .NET

Advanced Command Query Responsibility Segregation patterns.

## Core Concepts

### Commands (Write Operations)

Commands modify state and return results:

```csharp
// Command with typed result
public sealed record CreateCycleCommand(
    Guid PatientId,
    CycleType Type,
    DateOnly StartDate
) : IRequest<Result<Guid>>;

// Command for operations that may fail
public sealed record AdvancePhaseCommand(Guid CycleId, CyclePhase NewPhase) 
    : IRequest<Result>;

// Command with no return (fire-and-forget notification)
public sealed record SendNotificationCommand(Guid UserId, string Message) 
    : IRequest;
```

### Queries (Read Operations)

Queries return data without side effects:

```csharp
public sealed record GetCycleQuery(Guid Id) : IRequest<CycleDto?>;

public sealed record GetCyclesQuery(
    Guid? PatientId,
    CyclePhase? Phase,
    int Page = 1,
    int PageSize = 20
) : IRequest<PagedResult<CycleListDto>>;

public sealed record GetCycleStatsQuery(Guid PatientId) 
    : IRequest<CycleStatsDto>;
```

## Handler Patterns

### Command Handler with Validation

```csharp
public sealed class CreateCycleHandler(
    ICycleRepository repository,
    IPatientRepository patientRepository,
    IUnitOfWork unitOfWork,
    ILogger<CreateCycleHandler> logger)
    : IRequestHandler<CreateCycleCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateCycleCommand request,
        CancellationToken ct)
    {
        // Validate business rules
        var patient = await patientRepository.GetByIdAsync(request.PatientId, ct);
        if (patient is null)
            return Result.NotFound($"Patient {request.PatientId} not found");

        if (patient.Status != PatientStatus.Active)
            return Result.Invalid("Cannot create cycle for inactive patient");

        // Check for overlapping cycles
        var existingCycles = await repository.GetActiveByPatientAsync(request.PatientId, ct);
        if (existingCycles.Any())
            return Result.Invalid("Patient already has an active cycle");

        // Create domain entity
        var cycle = TreatmentCycle.Create(
            request.PatientId,
            request.Type,
            request.StartDate);

        await repository.AddAsync(cycle, ct);
        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation(
            "Created cycle {CycleId} for patient {PatientId}",
            cycle.Id, request.PatientId);

        return Result.Ok(cycle.Id);
    }
}
```

### Query Handler with Projection

```csharp
public sealed class GetCyclesHandler(IApplicationDbContext context)
    : IRequestHandler<GetCyclesQuery, PagedResult<CycleListDto>>
{
    public async Task<PagedResult<CycleListDto>> Handle(
        GetCyclesQuery request,
        CancellationToken ct)
    {
        var query = context.TreatmentCycles
            .AsNoTracking()
            .Include(c => c.Patient)
            .AsQueryable();

        // Apply filters
        if (request.PatientId.HasValue)
            query = query.Where(c => c.PatientId == request.PatientId.Value);

        if (request.Phase.HasValue)
            query = query.Where(c => c.CurrentPhase == request.Phase.Value);

        // Get total count
        var total = await query.CountAsync(ct);

        // Project to DTO and paginate
        var items = await query
            .OrderByDescending(c => c.StartDate)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(c => new CycleListDto(
                c.Id,
                c.Patient.FullName,
                c.Type.ToString(),
                c.CurrentPhase.ToString(),
                c.StartDate,
                c.Events.Count))
            .ToListAsync(ct);

        return new PagedResult<CycleListDto>(
            items, total, request.Page, request.PageSize);
    }
}
```

## Pipeline Behaviors

### Validation Behavior

```csharp
public sealed class ValidationBehavior<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        if (!validators.Any())
            return await next();

        var context = new ValidationContext<TRequest>(request);
        var failures = await Task.WhenAll(
            validators.Select(v => v.ValidateAsync(context, ct)));

        var errors = failures
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (errors.Count != 0)
            throw new ValidationException(errors);

        return await next();
    }
}
```

### Logging Behavior

```csharp
public sealed class LoggingBehavior<TRequest, TResponse>(
    ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        var requestName = typeof(TRequest).Name;
        
        logger.LogInformation(
            "Handling {RequestName} {@Request}",
            requestName, request);

        var sw = Stopwatch.StartNew();
        try
        {
            var response = await next();
            sw.Stop();

            logger.LogInformation(
                "Handled {RequestName} in {ElapsedMs}ms",
                requestName, sw.ElapsedMilliseconds);

            return response;
        }
        catch (Exception ex)
        {
            sw.Stop();
            logger.LogError(ex,
                "Error handling {RequestName} after {ElapsedMs}ms",
                requestName, sw.ElapsedMilliseconds);
            throw;
        }
    }
}
```

### Transaction Behavior

```csharp
public sealed class TransactionBehavior<TRequest, TResponse>(
    IApplicationDbContext context)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        // Skip for queries
        if (request is IQuery)
            return await next();

        await using var transaction = await context.Database
            .BeginTransactionAsync(ct);

        try
        {
            var response = await next();
            await transaction.CommitAsync(ct);
            return response;
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }
}
```

## Domain Events

### Event Definition

```csharp
public interface IDomainEvent
{
    DateTime OccurredAt { get; }
}

public sealed record CycleCreatedEvent(Guid CycleId, Guid PatientId) 
    : IDomainEvent
{
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}

public sealed record CyclePhaseChangedEvent(
    Guid CycleId,
    CyclePhase PreviousPhase,
    CyclePhase NewPhase) : IDomainEvent
{
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
```

### Event Dispatcher

```csharp
public sealed class DomainEventDispatcher(IMediator mediator)
{
    public async Task DispatchEventsAsync(
        IEnumerable<BaseEntity> entities,
        CancellationToken ct)
    {
        var domainEvents = entities
            .SelectMany(e => e.DomainEvents)
            .ToList();

        foreach (var entity in entities)
            entity.ClearDomainEvents();

        foreach (var domainEvent in domainEvents)
        {
            await mediator.Publish(domainEvent, ct);
        }
    }
}

// In UnitOfWork
public async Task<int> SaveChangesAsync(CancellationToken ct = default)
{
    var entities = _context.ChangeTracker
        .Entries<BaseEntity>()
        .Where(e => e.Entity.DomainEvents.Any())
        .Select(e => e.Entity)
        .ToList();

    var result = await _context.SaveChangesAsync(ct);

    await _eventDispatcher.DispatchEventsAsync(entities, ct);

    return result;
}
```

### Event Handler

```csharp
public sealed class CycleCreatedEventHandler(
    INotificationService notifications,
    IAuditService audit)
    : INotificationHandler<CycleCreatedEvent>
{
    public async Task Handle(CycleCreatedEvent notification, CancellationToken ct)
    {
        // Send notification
        await notifications.SendAsync(
            notification.PatientId,
            "New treatment cycle started",
            ct);

        // Audit log
        await audit.LogAsync(
            "CycleCreated",
            notification.CycleId.ToString(),
            ct);
    }
}
```

## Result Pattern

```csharp
public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public IReadOnlyList<string> Errors { get; }

    protected Result(bool isSuccess, IEnumerable<string>? errors = null)
    {
        IsSuccess = isSuccess;
        Errors = errors?.ToList() ?? [];
    }

    public static Result Ok() => new(true);
    public static Result<T> Ok<T>(T value) => new(value);
    public static Result Fail(params string[] errors) => new(false, errors);
    public static Result NotFound(string message) => new(false, [message]);
    public static Result Invalid(string message) => new(false, [message]);
}

public class Result<T> : Result
{
    public T? Value { get; }

    internal Result(T value) : base(true) => Value = value;
    internal Result(IEnumerable<string> errors) : base(false, errors) { }

    public static implicit operator Result<T>(T value) => new(value);
}
```

## Registration

```csharp
services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(CreateCycleCommand).Assembly);
    
    // Pipeline behaviors (order matters)
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(TransactionBehavior<,>));
});

services.AddValidatorsFromAssembly(typeof(CreateCycleCommandValidator).Assembly);
```
