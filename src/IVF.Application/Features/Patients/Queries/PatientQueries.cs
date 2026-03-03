using IVF.Application.Common;
using IVF.Application.Common.Behaviors;
using IVF.Application.Common.Interfaces;
using IVF.Application.Features.Patients.Commands;
using IVF.Domain.Enums;
using MediatR;

namespace IVF.Application.Features.Patients.Queries;

// ==================== GET PATIENT BY ID ====================
public record GetPatientByIdQuery(Guid Id) : IRequest<Result<PatientDto>>, IFieldAccessProtected
{
    public string TableName => "patients";
}

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
    : IRequest<PagedResult<PatientDto>>, IFieldAccessProtected
{
    public string TableName => "patients";
}

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

// ==================== ADVANCED SEARCH PATIENTS ====================
public record AdvancedSearchPatientsQuery(
    string? Query,
    string? Gender,
    PatientType? PatientType,
    PatientStatus? Status,
    PatientPriority? Priority,
    RiskLevel? RiskLevel,
    string? BloodType,
    DateTime? DobFrom,
    DateTime? DobTo,
    DateTime? CreatedFrom,
    DateTime? CreatedTo,
    string? SortBy,
    bool SortDescending = true,
    int Page = 1,
    int PageSize = 20
) : IRequest<PagedResult<PatientDto>>, IFieldAccessProtected
{
    public string TableName => "patients";
}

public class AdvancedSearchPatientsHandler : IRequestHandler<AdvancedSearchPatientsQuery, PagedResult<PatientDto>>
{
    private readonly IPatientRepository _patientRepo;

    public AdvancedSearchPatientsHandler(IPatientRepository patientRepo) => _patientRepo = patientRepo;

    public async Task<PagedResult<PatientDto>> Handle(AdvancedSearchPatientsQuery request, CancellationToken ct)
    {
        var (items, total) = await _patientRepo.AdvancedSearchAsync(
            request.Query, request.Gender, request.PatientType, request.Status,
            request.Priority, request.RiskLevel, request.BloodType,
            request.DobFrom, request.DobTo, request.CreatedFrom, request.CreatedTo,
            request.SortBy, request.SortDescending, request.Page, request.PageSize, ct);
        var dtos = items.Select(PatientDto.FromEntity).ToList();
        return new PagedResult<PatientDto>(dtos, total, request.Page, request.PageSize);
    }
}

// ==================== PATIENT ANALYTICS ====================
public record GetPatientAnalyticsQuery : IRequest<Result<PatientAnalyticsDto>>;

public record PatientAnalyticsDto(
    int TotalPatients,
    int ActivePatients,
    int InactivePatients,
    Dictionary<string, int> ByGender,
    Dictionary<string, int> ByType,
    Dictionary<string, int> ByAgeGroup,
    Dictionary<string, int> ByRiskLevel,
    Dictionary<string, int> RegistrationTrend,
    IReadOnlyList<PatientDto> RecentPatients
);

public class GetPatientAnalyticsHandler : IRequestHandler<GetPatientAnalyticsQuery, Result<PatientAnalyticsDto>>
{
    private readonly IPatientRepository _patientRepo;

    public GetPatientAnalyticsHandler(IPatientRepository patientRepo) => _patientRepo = patientRepo;

    public async Task<Result<PatientAnalyticsDto>> Handle(GetPatientAnalyticsQuery request, CancellationToken ct)
    {
        var total = await _patientRepo.GetTotalCountAsync(ct);
        var active = await _patientRepo.GetCountByStatusAsync(PatientStatus.Active, ct);
        var inactive = await _patientRepo.GetCountByStatusAsync(PatientStatus.Inactive, ct);
        var byGender = await _patientRepo.GetPatientsByGenderAsync(ct);
        var byType = await _patientRepo.GetPatientsByTypeAsync(ct);
        var byAge = await _patientRepo.GetPatientsByAgeGroupAsync(ct);
        var byRisk = await _patientRepo.GetPatientsByRiskLevelAsync(ct);
        var trend = await _patientRepo.GetPatientsRegistrationTrendAsync(12, ct);
        var recent = await _patientRepo.GetRecentPatientsAsync(10, ct);

        return Result<PatientAnalyticsDto>.Success(new PatientAnalyticsDto(
            total, active, inactive, byGender, byType, byAge, byRisk, trend,
            recent.Select(PatientDto.FromEntity).ToList()));
    }
}

// ==================== PATIENT AUDIT TRAIL ====================
public record GetPatientAuditTrailQuery(Guid PatientId, int Page = 1, int PageSize = 50) : IRequest<Result<PatientAuditTrailDto>>;

public record PatientAuditEntryDto(
    Guid Id,
    string Action,
    string? Username,
    string? OldValues,
    string? NewValues,
    string? ChangedColumns,
    string? IpAddress,
    DateTime CreatedAt
);

public record PatientAuditTrailDto(
    Guid PatientId,
    IReadOnlyList<PatientAuditEntryDto> Entries,
    int TotalCount
);

public class GetPatientAuditTrailHandler : IRequestHandler<GetPatientAuditTrailQuery, Result<PatientAuditTrailDto>>
{
    private readonly IAuditLogRepository _auditLogRepo;

    public GetPatientAuditTrailHandler(IAuditLogRepository auditLogRepo) => _auditLogRepo = auditLogRepo;

    public async Task<Result<PatientAuditTrailDto>> Handle(GetPatientAuditTrailQuery request, CancellationToken ct)
    {
        var logs = await _auditLogRepo.GetByEntityAsync("Patient", request.PatientId, ct);
        var entries = logs
            .OrderByDescending(l => l.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(l => new PatientAuditEntryDto(
                l.Id, l.Action, l.Username, l.OldValues, l.NewValues,
                l.ChangedColumns, l.IpAddress, l.CreatedAt))
            .ToList();

        return Result<PatientAuditTrailDto>.Success(
            new PatientAuditTrailDto(request.PatientId, entries, logs.Count));
    }
}

// ==================== PATIENTS REQUIRING FOLLOW-UP ====================
public record GetPatientsFollowUpQuery(int DaysSinceLastVisit = 90) : IRequest<Result<IReadOnlyList<PatientDto>>>;

public class GetPatientsFollowUpHandler : IRequestHandler<GetPatientsFollowUpQuery, Result<IReadOnlyList<PatientDto>>>
{
    private readonly IPatientRepository _patientRepo;

    public GetPatientsFollowUpHandler(IPatientRepository patientRepo) => _patientRepo = patientRepo;

    public async Task<Result<IReadOnlyList<PatientDto>>> Handle(GetPatientsFollowUpQuery request, CancellationToken ct)
    {
        var patients = await _patientRepo.GetPatientsRequiringFollowUpAsync(request.DaysSinceLastVisit, ct);
        return Result<IReadOnlyList<PatientDto>>.Success(patients.Select(PatientDto.FromEntity).ToList());
    }
}

// ==================== GDPR DATA RETENTION CHECK ====================
public record GetExpiredDataRetentionQuery : IRequest<Result<IReadOnlyList<PatientDto>>>;

public class GetExpiredDataRetentionHandler : IRequestHandler<GetExpiredDataRetentionQuery, Result<IReadOnlyList<PatientDto>>>
{
    private readonly IPatientRepository _patientRepo;

    public GetExpiredDataRetentionHandler(IPatientRepository patientRepo) => _patientRepo = patientRepo;

    public async Task<Result<IReadOnlyList<PatientDto>>> Handle(GetExpiredDataRetentionQuery request, CancellationToken ct)
    {
        var patients = await _patientRepo.GetExpiredDataRetentionAsync(ct);
        return Result<IReadOnlyList<PatientDto>>.Success(patients.Select(PatientDto.FromEntity).ToList());
    }
}
