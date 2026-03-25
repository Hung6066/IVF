using Microsoft.EntityFrameworkCore;

namespace IVF.Infrastructure.Persistence;

/// <summary>
/// Generates sequential, collision-safe business codes (e.g. BN-2026-000001).
/// Uses a PostgreSQL advisory transaction lock on the prefix to serialise concurrent
/// inserts, falling back to an optimistic retry loop for other databases.
/// </summary>
public static class CodeGenerator
{
    private const int MaxRetries = 5;

    /// <summary>
    /// Returns the next sequential code for the given prefix, e.g.
    ///   NextAsync(db, "SELECT MAX(...)", "CK-2026-", 4) → "CK-2026-0042"
    /// </summary>
    /// <param name="context">EF Core DbContext (must expose <see cref="IvfDbContext"/>).</param>
    /// <param name="getMaxCode">Async delegate to query the current MAX code string for the prefix.</param>
    /// <param name="prefix">Fixed prefix including the date segment, e.g. "CK-2026-".</param>
    /// <param name="padding">Zero-padding width of the numeric suffix.</param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task<string> NextAsync(
        IvfDbContext context,
        Func<Task<string?>> getMaxCode,
        string prefix,
        int padding,
        CancellationToken ct = default)
    {
        // Acquire a PostgreSQL advisory lock scoped to this transaction.
        // hashtext() maps the prefix string to a consistent int8 key.
        // This serialises all concurrent code generation for the same prefix.
        try
        {
            await context.Database.ExecuteSqlRawAsync(
                "SELECT pg_advisory_xact_lock(hashtext({0}))", [prefix]);
        }
        catch
        {
            // Non-PostgreSQL or lock not available — proceed optimistically.
        }

        var maxCode = await getMaxCode();
        int next = 1;
        if (maxCode != null
            && maxCode.Length == prefix.Length + padding
            && int.TryParse(maxCode[prefix.Length..], out var n))
        {
            next = n + 1;
        }

        return $"{prefix}{next.ToString().PadLeft(padding, '0')}";
    }

    /// <summary>
    /// Wraps a SaveChangesAsync call with retry logic on unique-key violations (Postgres 23505).
    /// Regenerates the code on each retry using the provided <paramref name="regenerate"/> delegate.
    /// </summary>
    public static async Task SaveWithRetryAsync(
        IvfDbContext context,
        Func<Task<string>> regenerate,
        Action<string> applyCode,
        CancellationToken ct = default)
    {
        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                await context.SaveChangesAsync(ct);
                return;
            }
            catch (DbUpdateException ex)
                when (IsUniqueViolation(ex) && attempt < MaxRetries - 1)
            {
                var newCode = await regenerate();
                applyCode(newCode);
            }
        }

        // Final attempt — let the exception propagate naturally.
        await context.SaveChangesAsync(ct);
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        var inner = ex.InnerException?.Message ?? string.Empty;
        // Npgsql: SQLSTATE 23505 — unique_violation
        return inner.Contains("23505") || inner.Contains("unique constraint") || inner.Contains("unique_violation");
    }
}
