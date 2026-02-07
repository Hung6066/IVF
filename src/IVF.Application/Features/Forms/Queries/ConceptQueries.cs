using IVF.Domain.Entities;
using MediatR;

namespace IVF.Application.Features.Forms.Queries;

/// <summary>
/// Get concept by ID with all mappings
/// </summary>
public record GetConceptByIdQuery(Guid Id) : IRequest<Concept?>;

/// <summary>
/// Full-text search concepts using PostgreSQL TsVector
/// </summary>
public record SearchConceptsQuery(
    string SearchTerm,
    int? ConceptType = null,
    int PageSize = 20,
    int PageNumber = 1
) : IRequest<SearchConceptsResult>;

public record SearchConceptsResult(
    List<ConceptDto> Concepts,
    int TotalCount
);

public record ConceptDto(
    Guid Id,
    string Code,
    string Display,
    string? Description,
    string System,
    int ConceptType,
    List<ConceptMappingDto> Mappings
);

public record ConceptMappingDto(
    Guid Id,
    string TargetSystem,
    string TargetCode,
    string TargetDisplay,
    string? Relationship
);

/// <summary>
/// Get all concepts by type
/// </summary>
public record GetConceptsByTypeQuery(
    int ConceptType,
    int PageSize = 50,
    int PageNumber = 1
) : IRequest<List<ConceptDto>>;
