using IVF.Application.Common;
using IVF.Application.Common.Interfaces;
using IVF.Application.Features.Patients.Commands;
using MediatR;

namespace IVF.Application.Features.Patients.Queries;

// ==================== GET PATIENT BY ID ====================
public record GetPatientByIdQuery(Guid Id) : IRequest<Result<PatientDto>>;

public class GetPatientByIdHandler : IRequestHandler<GetPatientByIdQuery, Result<PatientDto>>
{
    private readonly IPatientRepository _patientRepo;

    public GetPatientByIdHandler(IPatientRepository patientRepo)
    {
        _patientRepo = patientRepo;
    }

    public async Task<Result<PatientDto>> Handle(GetPatientByIdQuery request, CancellationToken ct)
    {
        var patient = await _patientRepo.GetByIdAsync(request.Id, ct);
        if (patient == null)
            return Result<PatientDto>.Failure("Patient not found");

        return Result<PatientDto>.Success(PatientDto.FromEntity(patient));
    }
}

// ==================== SEARCH PATIENTS ====================
public record SearchPatientsQuery(string? Query, string? Gender = null, int Page = 1, int PageSize = 20) 
    : IRequest<PagedResult<PatientDto>>;

public class SearchPatientsHandler : IRequestHandler<SearchPatientsQuery, PagedResult<PatientDto>>
{
    private readonly IPatientRepository _patientRepo;

    public SearchPatientsHandler(IPatientRepository patientRepo)
    {
        _patientRepo = patientRepo;
    }

    public async Task<PagedResult<PatientDto>> Handle(SearchPatientsQuery request, CancellationToken ct)
    {
        var (items, total) = await _patientRepo.SearchAsync(request.Query, request.Gender, request.Page, request.PageSize, ct);
        var dtos = items.Select(PatientDto.FromEntity).ToList();
        return new PagedResult<PatientDto>(dtos, total, request.Page, request.PageSize);
    }
}
