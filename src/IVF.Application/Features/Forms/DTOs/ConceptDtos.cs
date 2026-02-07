namespace IVF.Application.Features.Forms.DTOs;

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
