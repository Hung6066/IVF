using IVF.Domain.Entities;
using MediatR;

namespace IVF.Application.Features.Forms.Commands;

/// <summary>
/// Create a new medical concept
/// </summary>
public record CreateConceptCommand(
    string Code,
    string Display,
    string? Description,
    string System,
    int ConceptType
) : IRequest<Concept>;

/// <summary>
/// Update an existing concept
/// </summary>
public record UpdateConceptCommand(
    Guid Id,
    string Display,
    string? Description
) : IRequest<Concept>;

/// <summary>
/// Add external terminology mapping to a concept
/// </summary>
public record AddConceptMappingCommand(
    Guid ConceptId,
    string TargetSystem,
    string TargetCode,
    string TargetDisplay,
    string? Relationship
) : IRequest<ConceptMapping>;

/// <summary>
/// Link form field to a concept
/// </summary>
public record LinkFieldToConceptCommand(
    Guid FieldId,
    Guid ConceptId
) : IRequest<FormField>;

/// <summary>
/// Link form field option to a concept
/// </summary>
public record LinkOptionToConceptCommand(
    Guid OptionId,
    Guid ConceptId
) : IRequest<FormFieldOption>;
