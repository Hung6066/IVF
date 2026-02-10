using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IVF.Infrastructure.Repositories;

public class ConceptRepository : IConceptRepository
{
    private readonly IvfDbContext _context;

    public ConceptRepository(IvfDbContext context)
    {
        _context = context;
    }

    public async Task<Concept?> GetByIdAsync(Guid id, bool includeMappings = true, CancellationToken ct = default)
    {
        var query = _context.Concepts.AsNoTracking().AsQueryable();
        if (includeMappings)
            query = query.Include(c => c.Mappings);

        return await query.FirstOrDefaultAsync(c => c.Id == id, ct);
    }

    public async Task<Concept?> GetByCodeAsync(string code, CancellationToken ct = default)
    {
        return await _context.Concepts
            .AsNoTracking()
            .Include(c => c.Mappings)
            .FirstOrDefaultAsync(c => c.Code == code, ct);
    }

    public async Task<List<Concept>> GetByTypeAsync(int conceptType, int pageSize = 50, int pageNumber = 1, CancellationToken ct = default)
    {
        return await _context.Concepts
            .AsNoTracking()
            .Include(c => c.Mappings)
            .Where(c => !c.IsDeleted && (int)c.ConceptType == conceptType)
            .OrderBy(c => c.Display)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    public async Task<(List<Concept> Items, int TotalCount)> SearchAsync(
        string? searchTerm,
        int? conceptType,
        int pageSize = 20,
        int pageNumber = 1,
        CancellationToken ct = default)
    {
        var query = _context.Concepts
            .AsNoTracking()
            .Include(c => c.Mappings)
            .Where(c => !c.IsDeleted);

        var hasSearchTerm = !string.IsNullOrWhiteSpace(searchTerm);

        // Full-text search using PostgreSQL TsVector - only when search term is provided
        if (hasSearchTerm)
        {
            var term = searchTerm!.Trim();
            // Add :* for prefix matching
            var tsQueryTerm = string.Join(" & ", term.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(t => t + ":*"));

            query = query.Where(c =>
                EF.Functions.ToTsVector("english",
                    (c.Code ?? "") + " " +
                    (c.Display ?? "") + " " +
                    (c.Description ?? ""))
                .Matches(EF.Functions.ToTsQuery("english", tsQueryTerm))
            );
        }

        // Filter by type
        if (conceptType.HasValue)
        {
            query = query.Where(c => (int)c.ConceptType == conceptType.Value);
        }

        var totalCount = await query.CountAsync(ct);

        // Order by relevance only when searching, otherwise by display name
        IQueryable<Concept> orderedQuery;
        if (hasSearchTerm)
        {
            var term = searchTerm!.Trim();
            var tsQueryTerm = string.Join(" & ", term.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(t => t + ":*"));

            orderedQuery = query.OrderByDescending(c =>
                EF.Functions.ToTsVector("english",
                    (c.Code ?? "") + " " +
                    (c.Display ?? "") + " " +
                    (c.Description ?? ""))
                .Rank(EF.Functions.ToTsQuery("english", tsQueryTerm))
            );
        }
        else
        {
            orderedQuery = query.OrderBy(c => c.Display);
        }

        var items = await orderedQuery
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<Concept> AddAsync(Concept concept, CancellationToken ct = default)
    {
        _context.Concepts.Add(concept);
        await _context.SaveChangesAsync(ct);
        return concept;
    }

    public async Task UpdateAsync(Concept concept, CancellationToken ct = default)
    {
        _context.Concepts.Update(concept);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Concepts.AnyAsync(c => c.Id == id, ct);
    }

    public async Task<ConceptMapping> AddMappingAsync(ConceptMapping mapping, CancellationToken ct = default)
    {
        _context.Set<ConceptMapping>().Add(mapping);
        await _context.SaveChangesAsync(ct);
        return mapping;
    }

    public async Task<FormField?> GetFieldByIdAsync(Guid fieldId, CancellationToken ct = default)
    {
        return await _context.FormFields.FirstOrDefaultAsync(f => f.Id == fieldId, ct);
    }

    public async Task UpdateFieldAsync(FormField field, CancellationToken ct = default)
    {
        _context.FormFields.Update(field);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<FormFieldOption?> GetFieldOptionByIdAsync(Guid optionId, CancellationToken ct = default)
    {
        return await _context.FormFieldOptions.FirstOrDefaultAsync(o => o.Id == optionId, ct);
    }

    public async Task UpdateFieldOptionAsync(FormFieldOption option, CancellationToken ct = default)
    {
        _context.FormFieldOptions.Update(option);
        await _context.SaveChangesAsync(ct);
    }
}
