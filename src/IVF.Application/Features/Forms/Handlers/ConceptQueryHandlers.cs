using IVF.Application.Common.Interfaces;
using IVF.Application.Features.Forms.Queries;
using IVF.Domain.Entities;
using MediatR;

namespace IVF.Application.Features.Forms.Handlers;

public class GetConceptByIdHandler : IRequestHandler<GetConceptByIdQuery, Concept?>
{
    private readonly IConceptRepository _repository;

    public GetConceptByIdHandler(IConceptRepository repository)
    {
        _repository = repository;
    }

    public async Task<Concept?> Handle(GetConceptByIdQuery request, CancellationToken cancellationToken)
    {
        return await _repository.GetByIdAsync(request.Id, true, cancellationToken);
    }
}

public class SearchConceptsHandler : IRequestHandler<SearchConceptsQuery, SearchConceptsResult>
{
    private readonly IConceptRepository _repository;

    public SearchConceptsHandler(IConceptRepository repository)
    {
        _repository = repository;
    }

    public async Task<SearchConceptsResult> Handle(SearchConceptsQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _repository.SearchAsync(
            request.SearchTerm,
            request.ConceptType,
            request.PageSize,
            request.PageNumber,
            cancellationToken
        );

        var concepts = items.Select(c => new ConceptDto(
            c.Id,
            c.Code,
            c.Display,
            c.Description,
            c.System,
            (int)c.ConceptType,
            c.Mappings.Select(m => new ConceptMappingDto(
                m.Id,
                m.TargetSystem,
                m.TargetCode,
                m.TargetDisplay,
                m.Relationship
            )).ToList()
        )).ToList();

        return new SearchConceptsResult(concepts, totalCount);
    }
}

public class GetConceptsByTypeHandler : IRequestHandler<GetConceptsByTypeQuery, List<ConceptDto>>
{
    private readonly IConceptRepository _repository;

    public GetConceptsByTypeHandler(IConceptRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<ConceptDto>> Handle(GetConceptsByTypeQuery request, CancellationToken cancellationToken)
    {
        var items = await _repository.GetByTypeAsync(
            request.ConceptType,
            request.PageSize,
            request.PageNumber,
            cancellationToken
        );

        return items.Select(c => new ConceptDto(
            c.Id,
            c.Code,
            c.Display,
            c.Description,
            c.System,
            (int)c.ConceptType,
            c.Mappings.Select(m => new ConceptMappingDto(
                m.Id,
                m.TargetSystem,
                m.TargetCode,
                m.TargetDisplay,
                m.Relationship
            )).ToList()
        )).ToList();
    }
}
