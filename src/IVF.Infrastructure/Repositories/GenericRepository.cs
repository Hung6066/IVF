using System.Linq.Expressions;
using IVF.Domain.Common;
using IVF.Infrastructure.Caching;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IVF.Infrastructure.Repositories;

/// <summary>
/// Generic repository interface for CRUD operations
/// </summary>
public interface IRepository<TEntity> where TEntity : BaseEntity
{
    // Read operations
    Task<TEntity?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<TEntity?> GetByIdAsync(Guid id, params Expression<Func<TEntity, object>>[] includes);
    Task<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<TEntity>> FindAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default);
    Task<TEntity?> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default);
    Task<PagedResult<TEntity>> GetPagedAsync(PaginationParams pagination, CancellationToken ct = default);
    Task<PagedResult<TEntity>> GetPagedAsync(Expression<Func<TEntity, bool>> predicate, PaginationParams pagination, CancellationToken ct = default);

    // Write operations
    Task<TEntity> AddAsync(TEntity entity, CancellationToken ct = default);
    Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken ct = default);
    Task UpdateAsync(TEntity entity, CancellationToken ct = default);
    Task UpdateRangeAsync(IEnumerable<TEntity> entities, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task DeleteAsync(TEntity entity, CancellationToken ct = default);
    Task DeleteRangeAsync(IEnumerable<TEntity> entities, CancellationToken ct = default);

    // Utility operations
    Task<bool> ExistsAsync(Guid id, CancellationToken ct = default);
    Task<bool> ExistsAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default);
    Task<int> CountAsync(CancellationToken ct = default);
    Task<int> CountAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default);

    // Queryable access (for complex queries)
    IQueryable<TEntity> Query();
    IQueryable<TEntity> QueryNoTracking();
}

/// <summary>
/// Generic repository implementation with caching and soft-delete support
/// </summary>
public class GenericRepository<TEntity> : IRepository<TEntity>
    where TEntity : BaseEntity
{
    protected readonly IvfDbContext _context;
    protected readonly DbSet<TEntity> _dbSet;
    protected readonly ICacheService _cache;
    protected readonly ILogger _logger;

    public GenericRepository(
        IvfDbContext context,
        ICacheService cache,
        ILogger<GenericRepository<TEntity>> logger)
    {
        _context = context;
        _dbSet = context.Set<TEntity>();
        _cache = cache;
        _logger = logger;
    }

    #region Read Operations

    public virtual async Task<TEntity?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _dbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, ct);
    }

    public virtual async Task<TEntity?> GetByIdAsync(
        Guid id,
        params Expression<Func<TEntity, object>>[] includes)
    {
        var query = _dbSet.AsNoTracking();

        foreach (var include in includes)
        {
            query = query.Include(include);
        }

        return await query.FirstOrDefaultAsync(e => e.Id == id);
    }

    public virtual async Task<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken ct = default)
    {
        return await _dbSet
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public virtual async Task<IReadOnlyList<TEntity>> FindAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken ct = default)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(predicate)
            .ToListAsync(ct);
    }

    public virtual async Task<TEntity?> FirstOrDefaultAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken ct = default)
    {
        return await _dbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(predicate, ct);
    }

    public virtual async Task<PagedResult<TEntity>> GetPagedAsync(
        PaginationParams pagination,
        CancellationToken ct = default)
    {
        var query = _dbSet.AsNoTracking();
        return await CreatePagedResultAsync(query, pagination, ct);
    }

    public virtual async Task<PagedResult<TEntity>> GetPagedAsync(
        Expression<Func<TEntity, bool>> predicate,
        PaginationParams pagination,
        CancellationToken ct = default)
    {
        var query = _dbSet.AsNoTracking().Where(predicate);
        return await CreatePagedResultAsync(query, pagination, ct);
    }

    protected async Task<PagedResult<TEntity>> CreatePagedResultAsync(
        IQueryable<TEntity> query,
        PaginationParams pagination,
        CancellationToken ct)
    {
        var totalCount = await query.CountAsync(ct);

        // Apply sorting
        if (!string.IsNullOrEmpty(pagination.SortBy))
        {
            query = ApplySorting(query, pagination.SortBy, pagination.SortDescending);
        }

        // Apply pagination
        var items = await query
            .Skip((pagination.Page - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .ToListAsync(ct);

        return new PagedResult<TEntity>(
            items,
            totalCount,
            pagination.Page,
            pagination.PageSize);
    }

    protected virtual IQueryable<TEntity> ApplySorting(
        IQueryable<TEntity> query,
        string sortBy,
        bool descending)
    {
        // Use reflection to get property
        var entityType = typeof(TEntity);
        var property = entityType.GetProperty(sortBy,
            System.Reflection.BindingFlags.IgnoreCase |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.Instance);

        if (property == null)
        {
            _logger.LogWarning("Sort property {SortBy} not found on {Entity}", sortBy, entityType.Name);
            return query;
        }

        var parameter = Expression.Parameter(entityType, "x");
        var propertyAccess = Expression.Property(parameter, property);
        var lambda = Expression.Lambda(propertyAccess, parameter);

        var methodName = descending ? "OrderByDescending" : "OrderBy";
        var resultExpression = Expression.Call(
            typeof(Queryable),
            methodName,
            new[] { entityType, property.PropertyType },
            query.Expression,
            Expression.Quote(lambda));

        return query.Provider.CreateQuery<TEntity>(resultExpression);
    }

    #endregion

    #region Write Operations

    public virtual async Task<TEntity> AddAsync(TEntity entity, CancellationToken ct = default)
    {
        await _dbSet.AddAsync(entity, ct);
        await _context.SaveChangesAsync(ct);

        _logger.LogDebug("Added {Entity} with Id {Id}", typeof(TEntity).Name, entity.Id);
        return entity;
    }

    public virtual async Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken ct = default)
    {
        var entityList = entities.ToList();

        await _dbSet.AddRangeAsync(entityList, ct);
        await _context.SaveChangesAsync(ct);

        _logger.LogDebug("Added {Count} {Entity} entities", entityList.Count, typeof(TEntity).Name);
    }

    public virtual async Task UpdateAsync(TEntity entity, CancellationToken ct = default)
    {
        entity.SetUpdated();
        _dbSet.Update(entity);
        await _context.SaveChangesAsync(ct);

        // Invalidate cache
        await InvalidateCacheAsync(entity.Id);

        _logger.LogDebug("Updated {Entity} with Id {Id}", typeof(TEntity).Name, entity.Id);
    }

    public virtual async Task UpdateRangeAsync(IEnumerable<TEntity> entities, CancellationToken ct = default)
    {
        var entityList = entities.ToList();

        foreach (var entity in entityList)
        {
            entity.SetUpdated();
        }

        _dbSet.UpdateRange(entityList);
        await _context.SaveChangesAsync(ct);

        // Invalidate cache for all entities
        foreach (var entity in entityList)
        {
            await InvalidateCacheAsync(entity.Id);
        }

        _logger.LogDebug("Updated {Count} {Entity} entities", entityList.Count, typeof(TEntity).Name);
    }

    public virtual async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _dbSet.FindAsync(new object[] { id }, ct);

        if (entity != null)
        {
            await DeleteAsync(entity, ct);
        }
    }

    public virtual async Task DeleteAsync(TEntity entity, CancellationToken ct = default)
    {
        // Soft delete using BaseEntity
        entity.MarkAsDeleted();
        _dbSet.Update(entity);

        await _context.SaveChangesAsync(ct);

        // Invalidate cache
        await InvalidateCacheAsync(entity.Id);

        _logger.LogDebug("Deleted {Entity} with Id {Id}", typeof(TEntity).Name, entity.Id);
    }

    public virtual async Task DeleteRangeAsync(IEnumerable<TEntity> entities, CancellationToken ct = default)
    {
        var entityList = entities.ToList();

        foreach (var entity in entityList)
        {
            entity.MarkAsDeleted();
        }

        _dbSet.UpdateRange(entityList);
        await _context.SaveChangesAsync(ct);

        // Invalidate cache for all entities
        foreach (var entity in entityList)
        {
            await InvalidateCacheAsync(entity.Id);
        }

        _logger.LogDebug("Deleted {Count} {Entity} entities", entityList.Count, typeof(TEntity).Name);
    }

    #endregion

    #region Utility Operations

    public virtual async Task<bool> ExistsAsync(Guid id, CancellationToken ct = default)
    {
        return await _dbSet.AnyAsync(e => e.Id == id, ct);
    }

    public virtual async Task<bool> ExistsAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken ct = default)
    {
        return await _dbSet.AnyAsync(predicate, ct);
    }

    public virtual async Task<int> CountAsync(CancellationToken ct = default)
    {
        return await _dbSet.CountAsync(ct);
    }

    public virtual async Task<int> CountAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken ct = default)
    {
        return await _dbSet.CountAsync(predicate, ct);
    }

    public IQueryable<TEntity> Query()
    {
        return _dbSet;
    }

    public IQueryable<TEntity> QueryNoTracking()
    {
        return _dbSet.AsNoTracking();
    }

    #endregion

    #region Cache Operations

    protected virtual string GetCacheKey(Guid id)
    {
        return $"ivf:{typeof(TEntity).Name.ToLower()}:{id}";
    }

    protected virtual async Task InvalidateCacheAsync(Guid id)
    {
        try
        {
            await _cache.RemoveAsync(GetCacheKey(id));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to invalidate cache for {Entity} {Id}",
                typeof(TEntity).Name, id);
        }
    }

    #endregion
}

/// <summary>
/// Cached repository for entities that benefit from caching
/// </summary>
public class CachedRepository<TEntity> : GenericRepository<TEntity>
    where TEntity : BaseEntity
{
    private readonly TimeSpan _cacheExpiry;

    public CachedRepository(
        IvfDbContext context,
        ICacheService cache,
        ILogger<CachedRepository<TEntity>> logger,
        TimeSpan? cacheExpiry = null) : base(context, cache, logger)
    {
        _cacheExpiry = cacheExpiry ?? TimeSpan.FromMinutes(15);
    }

    public override async Task<TEntity?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var cacheKey = GetCacheKey(id);

        return await _cache.GetOrSetAsync(
            cacheKey,
            async () => await base.GetByIdAsync(id, ct),
            _cacheExpiry,
            ct);
    }
}

/// <summary>
/// Pagination parameters
/// </summary>
public record PaginationParams
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? SortBy { get; init; }
    public bool SortDescending { get; init; }
    public string? SearchTerm { get; init; }

    public static PaginationParams Default => new();

    public static PaginationParams Create(int page = 1, int pageSize = 20, string? sortBy = null, bool sortDescending = false)
    {
        return new PaginationParams
        {
            Page = Math.Max(1, page),
            PageSize = Math.Clamp(pageSize, 1, 100),
            SortBy = sortBy,
            SortDescending = sortDescending
        };
    }
}

/// <summary>
/// Paged result container
/// </summary>
public record PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; }
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;

    public PagedResult(IReadOnlyList<T> items, int totalCount, int page, int pageSize)
    {
        Items = items;
        TotalCount = totalCount;
        Page = page;
        PageSize = pageSize;
    }

    public PagedResult<TResult> Map<TResult>(Func<T, TResult> mapper)
    {
        return new PagedResult<TResult>(
            Items.Select(mapper).ToList(),
            TotalCount,
            Page,
            PageSize);
    }
}
