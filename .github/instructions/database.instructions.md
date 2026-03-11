---
description: "Use when writing or modifying EF Core database configurations, migrations, repository implementations, and query optimization. Enforces PostgreSQL conventions, soft delete, multi-tenancy query filters, and indexing standards for the IVF .NET 10 project."
applyTo: "src/IVF.Infrastructure/**"
---

# Database & EF Core Conventions

## Entity Configuration

File: `src/IVF.Infrastructure/Persistence/Configurations/{Entity}Configuration.cs`

```csharp
public class PatientConfiguration : IEntityTypeConfiguration<Patient>
{
    public void Configure(EntityTypeBuilder<Patient> builder)
    {
        builder.ToTable("patients");  // lowercase, plural

        builder.HasKey(p => p.Id);

        // Soft delete query filter — ALWAYS required
        builder.HasQueryFilter(p => !p.IsDeleted);

        // String lengths
        builder.Property(p => p.FullName).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Phone).HasMaxLength(20);

        // Enum → string conversion
        builder.Property(p => p.Gender).HasConversion<string>().HasMaxLength(20);

        // Indexes — add for frequently queried columns
        builder.HasIndex(p => p.PatientCode).IsUnique();
        builder.HasIndex(p => p.TenantId);
        builder.HasIndex(p => new { p.TenantId, p.FullName }); // composite for tenant-scoped search

        // FK relationships — ALWAYS Restrict, never Cascade
        builder.HasMany(p => p.Cycles)
            .WithOne(c => c.Patient)
            .HasForeignKey(c => c.PatientId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
```

## Key Rules

1. **Table names**: lowercase, plural (`"patients"`, `"treatment_cycles"`, `"audit_logs"`)
2. **Soft delete**: Every configuration MUST have `HasQueryFilter(e => !e.IsDeleted)`
3. **Tenant index**: Every `ITenantEntity` MUST have `HasIndex(e => e.TenantId)`
4. **FK behavior**: `OnDelete(DeleteBehavior.Restrict)` — never `Cascade` or `SetNull`
5. **Enum storage**: `.HasConversion<string>()` — store as text, not integer
6. **String lengths**: Always specify `HasMaxLength()` — no unbounded `nvarchar(max)`
7. **Indexes**: Add for columns used in `WHERE`, `ORDER BY`, or `JOIN`
8. **Composite indexes**: `HasIndex(e => new { e.TenantId, e.Field })` for tenant-scoped queries

## Migration Conventions

```bash
# Generate migration (run from repo root)
dotnet ef migrations add Add{EntityName} \
  --project src/IVF.Infrastructure \
  --startup-project src/IVF.API

# Apply migration
dotnet ef database update \
  --project src/IVF.Infrastructure \
  --startup-project src/IVF.API
```

- **Naming**: `Add{Entity}`, `Update{Feature}Schema`, `Add{Column}To{Table}`
- **One logical change per migration** — don't bundle unrelated schema changes
- **Review generated SQL** before applying: `dotnet ef migrations script`
- **Never edit applied migrations** — create a new corrective migration instead
- **Dev auto-migration**: `DatabaseSeeder.SeedAsync()` runs on startup — no manual `database update` needed in dev

## Repository Pattern

### Interface (Application layer)

```csharp
// File: src/IVF.Application/Common/Interfaces/I{Entity}Repository.cs
public interface IPatientRepository
{
    Task<Patient?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Patient> AddAsync(Patient entity, CancellationToken ct = default);
    Task<(IReadOnlyList<Patient> Items, int Total)> SearchAsync(
        string? query, int page, int pageSize, CancellationToken ct = default);
    Task UpdateAsync(Patient entity, CancellationToken ct = default);
}
```

### Implementation (Infrastructure layer)

```csharp
// File: src/IVF.Infrastructure/Repositories/{Entity}Repository.cs
public class PatientRepository : IPatientRepository
{
    private readonly IvfDbContext _context;

    public PatientRepository(IvfDbContext context) => _context = context;

    public async Task<Patient?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.Patients
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<(IReadOnlyList<Patient> Items, int Total)> SearchAsync(
        string? query, int page, int pageSize, CancellationToken ct = default)
    {
        var q = _context.Patients.AsQueryable();

        if (!string.IsNullOrWhiteSpace(query))
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

### Registration

```csharp
// File: src/IVF.Infrastructure/DependencyInjection.cs
services.AddScoped<IPatientRepository, PatientRepository>();
```

## Query Performance Rules

1. **Filter in DB, not memory** — Never `ToListAsync()` then `.Where()` in C#
2. **Select only needed columns** for read-heavy queries — use `.Select()` projection
3. **Use `.Include()` explicitly** — no lazy loading (it's disabled)
4. **Avoid N+1** — include related data in a single query
5. **Pagination required** for all list queries — never return unbounded results
6. **Use `AsNoTracking()`** for read-only queries (queries, not commands)
7. **Count separately** — `CountAsync()` before `Skip/Take` for paged results

## DbContext Rules

- 90+ `DbSet<T>` properties registered
- Multi-tenancy via `SetCurrentTenant(tenantId)` — applied by `TenantResolutionMiddleware`
- Audit columns (`CreatedAt`, `UpdatedAt`) auto-set in `SaveChangesAsync` override
- Soft delete: `MarkAsDeleted()` sets `IsDeleted = true` — never hard delete

## PostgreSQL-Specific

- Connection: `Host=localhost;Port=5433;Database=ivf_db;Username=postgres;Password=postgres`
- JSON columns: `HasColumnType("jsonb")` for flexible schema fields
- Full-text search: Use `EF.Functions.ILike()` for case-insensitive LIKE
- Partitioning: `AuditLog` table partitioned by month (auto-created)
- Streaming replication: primary → standby (read-replica routing in production)
