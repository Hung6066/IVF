using IVF.Domain.Entities;

namespace IVF.Application.Common.Interfaces;

public interface IConceptRepository
{
    // Concepts
    Task<Concept?> GetByIdAsync(Guid id, bool includeMappings = true, CancellationToken ct = default);
    Task<Concept?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<List<Concept>> GetByTypeAsync(int conceptType, int pageSize = 50, int pageNumber = 1, CancellationToken ct = default);
    Task<(List<Concept> Items, int TotalCount)> SearchAsync(
        string? searchTerm,
        int? conceptType,
        int pageSize = 20,
        int pageNumber = 1,
        CancellationToken ct = default);
    Task<Concept> AddAsync(Concept concept, CancellationToken ct = default);
    Task UpdateAsync(Concept concept, CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken ct = default);
    
    // Concept Mappings
    Task<ConceptMapping> AddMappingAsync(ConceptMapping mapping, CancellationToken ct = default);
    
    // Field/Option linking
    Task<FormField?> GetFieldByIdAsync(Guid fieldId, CancellationToken ct = default);
    Task UpdateFieldAsync(FormField field, CancellationToken ct = default);
    Task<FormFieldOption?> GetFieldOptionByIdAsync(Guid optionId, CancellationToken ct = default);
    Task UpdateFieldOptionAsync(FormFieldOption option, CancellationToken ct = default);
}
