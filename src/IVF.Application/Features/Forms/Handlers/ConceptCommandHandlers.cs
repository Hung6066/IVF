using IVF.Application.Common.Interfaces;
using IVF.Application.Features.Forms.Commands;
using IVF.Application.Features.Forms.DTOs;
using IVF.Domain.Entities;
using MediatR;

namespace IVF.Application.Features.Forms.Handlers;

public class CreateConceptHandler : IRequestHandler<CreateConceptCommand, ConceptDto>
{
    private readonly IConceptRepository _repository;

    public CreateConceptHandler(IConceptRepository repository)
    {
        _repository = repository;
    }

    public async Task<ConceptDto> Handle(CreateConceptCommand request, CancellationToken cancellationToken)
    {
        var concept = Concept.Create(
            request.Code,
            request.Display,
            request.Description,
            request.System,
            (ConceptType)request.ConceptType
        );

        await _repository.AddAsync(concept, cancellationToken);
        
        return new ConceptDto(
            concept.Id, concept.Code, concept.Display, concept.Description, 
            concept.System, (int)concept.ConceptType, new List<ConceptMappingDto>());
    }
}

public class UpdateConceptHandler : IRequestHandler<UpdateConceptCommand, ConceptDto>
{
    private readonly IConceptRepository _repository;

    public UpdateConceptHandler(IConceptRepository repository)
    {
        _repository = repository;
    }

    public async Task<ConceptDto> Handle(UpdateConceptCommand request, CancellationToken cancellationToken)
    {
        var concept = await _repository.GetByIdAsync(request.Id, true, cancellationToken)
            ?? throw new Exception("Concept not found");

        concept.Update(request.Display, request.Description);
        await _repository.UpdateAsync(concept, cancellationToken);

        return new ConceptDto(
            concept.Id,
            concept.Code,
            concept.Display,
            concept.Description,
            concept.System,
            (int)concept.ConceptType,
            concept.Mappings.Select(m => new ConceptMappingDto(
                m.Id, m.TargetSystem, m.TargetCode, m.TargetDisplay, m.Relationship
            )).ToList()
        );
    }
}

public class AddConceptMappingHandler : IRequestHandler<AddConceptMappingCommand, ConceptMappingDto>
{
    private readonly IConceptRepository _repository;

    public AddConceptMappingHandler(IConceptRepository repository)
    {
        _repository = repository;
    }

    public async Task<ConceptMappingDto> Handle(AddConceptMappingCommand request, CancellationToken cancellationToken)
    {
        var concept = await _repository.GetByIdAsync(request.ConceptId, true, cancellationToken)
            ?? throw new Exception("Concept not found");

        var mapping = concept.AddMapping(
            request.TargetSystem,
            request.TargetCode,
            request.TargetDisplay,
            request.Relationship
        );

        await _repository.UpdateAsync(concept, cancellationToken);

        return new ConceptMappingDto(
            mapping.Id, mapping.TargetSystem, mapping.TargetCode, mapping.TargetDisplay, mapping.Relationship);
    }
}

public class LinkFieldToConceptHandler : IRequestHandler<LinkFieldToConceptCommand, FormFieldDto>
{
    private readonly IConceptRepository _repository;

    public LinkFieldToConceptHandler(IConceptRepository repository)
    {
        _repository = repository;
    }

    public async Task<FormFieldDto> Handle(LinkFieldToConceptCommand request, CancellationToken cancellationToken)
    {
        var f = await _repository.GetFieldByIdAsync(request.FieldId, cancellationToken)
            ?? throw new Exception("Form field not found");

        var conceptExists = await _repository.ExistsAsync(request.ConceptId, cancellationToken);
        if (!conceptExists)
            throw new Exception("Concept not found");

        f.LinkToConcept(request.ConceptId);
        await _repository.UpdateFieldAsync(f, cancellationToken);

        return new FormFieldDto(
            f.Id, f.FieldKey, f.Label, f.Placeholder, f.FieldType, f.DisplayOrder,
            f.IsRequired, f.OptionsJson, f.ValidationRulesJson, f.DefaultValue,
            f.HelpText, f.ConditionalLogicJson, f.ConceptId);
    }
}

public class LinkOptionToConceptHandler : IRequestHandler<LinkOptionToConceptCommand, bool>
{
    private readonly IConceptRepository _repository;

    public LinkOptionToConceptHandler(IConceptRepository repository)
    {
        _repository = repository;
    }

    public async Task<bool> Handle(LinkOptionToConceptCommand request, CancellationToken cancellationToken)
    {
        var option = await _repository.GetFieldOptionByIdAsync(request.OptionId, cancellationToken)
            ?? throw new Exception("Form field option not found");

        var conceptExists = await _repository.ExistsAsync(request.ConceptId, cancellationToken);
        if (!conceptExists)
            throw new Exception("Concept not found");

        option.LinkToConcept(request.ConceptId);
        await _repository.UpdateFieldOptionAsync(option, cancellationToken);

        return true;
    }
}
