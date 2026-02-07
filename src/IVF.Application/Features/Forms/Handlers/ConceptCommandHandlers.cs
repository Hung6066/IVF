using IVF.Application.Common.Interfaces;
using IVF.Application.Features.Forms.Commands;
using IVF.Domain.Entities;
using MediatR;

namespace IVF.Application.Features.Forms.Handlers;

public class CreateConceptHandler : IRequestHandler<CreateConceptCommand, Concept>
{
    private readonly IConceptRepository _repository;

    public CreateConceptHandler(IConceptRepository repository)
    {
        _repository = repository;
    }

    public async Task<Concept> Handle(CreateConceptCommand request, CancellationToken cancellationToken)
    {
        var concept = Concept.Create(
            request.Code,
            request.Display,
            request.Description,
            request.System,
            (ConceptType)request.ConceptType
        );

        return await _repository.AddAsync(concept, cancellationToken);
    }
}

public class UpdateConceptHandler : IRequestHandler<UpdateConceptCommand, Concept>
{
    private readonly IConceptRepository _repository;

    public UpdateConceptHandler(IConceptRepository repository)
    {
        _repository = repository;
    }

    public async Task<Concept> Handle(UpdateConceptCommand request, CancellationToken cancellationToken)
    {
        var concept = await _repository.GetByIdAsync(request.Id, false, cancellationToken)
            ?? throw new Exception("Concept not found");

        concept.Update(request.Display, request.Description);
        await _repository.UpdateAsync(concept, cancellationToken);

        return concept;
    }
}

public class AddConceptMappingHandler : IRequestHandler<AddConceptMappingCommand, ConceptMapping>
{
    private readonly IConceptRepository _repository;

    public AddConceptMappingHandler(IConceptRepository repository)
    {
        _repository = repository;
    }

    public async Task<ConceptMapping> Handle(AddConceptMappingCommand request, CancellationToken cancellationToken)
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

        return mapping;
    }
}

public class LinkFieldToConceptHandler : IRequestHandler<LinkFieldToConceptCommand, FormField>
{
    private readonly IConceptRepository _repository;

    public LinkFieldToConceptHandler(IConceptRepository repository)
    {
        _repository = repository;
    }

    public async Task<FormField> Handle(LinkFieldToConceptCommand request, CancellationToken cancellationToken)
    {
        var field = await _repository.GetFieldByIdAsync(request.FieldId, cancellationToken)
            ?? throw new Exception("Form field not found");

        var conceptExists = await _repository.ExistsAsync(request.ConceptId, cancellationToken);
        if (!conceptExists)
            throw new Exception("Concept not found");

        field.LinkToConcept(request.ConceptId);
        await _repository.UpdateFieldAsync(field, cancellationToken);

        return field;
    }
}

public class LinkOptionToConceptHandler : IRequestHandler<LinkOptionToConceptCommand, FormFieldOption>
{
    private readonly IConceptRepository _repository;

    public LinkOptionToConceptHandler(IConceptRepository repository)
    {
        _repository = repository;
    }

    public async Task<FormFieldOption> Handle(LinkOptionToConceptCommand request, CancellationToken cancellationToken)
    {
        var option = await _repository.GetFieldOptionByIdAsync(request.OptionId, cancellationToken)
            ?? throw new Exception("Form field option not found");

        var conceptExists = await _repository.ExistsAsync(request.ConceptId, cancellationToken);
        if (!conceptExists)
            throw new Exception("Concept not found");

        option.LinkToConcept(request.ConceptId);
        await _repository.UpdateFieldOptionAsync(option, cancellationToken);

        return option;
    }
}
