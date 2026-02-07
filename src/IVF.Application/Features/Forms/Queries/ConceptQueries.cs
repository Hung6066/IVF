using IVF.Application.Features.Forms.DTOs;
using IVF.Domain.Entities;
using MediatR;

namespace IVF.Application.Features.Forms.Queries;

/// <summary>
/// Get concept by ID with all mappings
/// </summary>
public record GetConceptByIdQuery(Guid Id) : IRequest<ConceptDto?>;

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

/// <summary>
/// Get all concepts by type
/// </summary>
public record GetConceptsByTypeQuery(
    int ConceptType,
    int PageSize = 50,
    int PageNumber = 1
) : IRequest<List<ConceptDto>>;
