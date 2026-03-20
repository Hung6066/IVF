using IVF.Application.Common.Interfaces;
using IVF.Application.Features.Consultations.Commands;
using MediatR;

namespace IVF.Application.Features.Consultations.Queries;

// ==================== GET BY ID ====================
public record GetConsultationByIdQuery(Guid Id) : IRequest<ConsultationDto?>;

public class GetConsultationByIdHandler : IRequestHandler<GetConsultationByIdQuery, ConsultationDto?>
{
    private readonly IConsultationRepository _consultationRepo;
    public GetConsultationByIdHandler(IConsultationRepository consultationRepo) => _consultationRepo = consultationRepo;

    public async Task<ConsultationDto?> Handle(GetConsultationByIdQuery request, CancellationToken ct)
    {
        var consultation = await _consultationRepo.GetByIdAsync(request.Id, ct);
        return consultation == null ? null : ConsultationDto.FromEntity(consultation);
    }
}

// ==================== GET BY PATIENT ====================
public record GetConsultationsByPatientQuery(Guid PatientId) : IRequest<IReadOnlyList<ConsultationDto>>;

public class GetConsultationsByPatientHandler : IRequestHandler<GetConsultationsByPatientQuery, IReadOnlyList<ConsultationDto>>
{
    private readonly IConsultationRepository _consultationRepo;
    public GetConsultationsByPatientHandler(IConsultationRepository consultationRepo) => _consultationRepo = consultationRepo;

    public async Task<IReadOnlyList<ConsultationDto>> Handle(GetConsultationsByPatientQuery request, CancellationToken ct)
    {
        var consultations = await _consultationRepo.GetByPatientIdAsync(request.PatientId, ct);
        return consultations.Select(ConsultationDto.FromEntity).ToList();
    }
}

// ==================== GET BY CYCLE ====================
public record GetConsultationsByCycleQuery(Guid CycleId) : IRequest<IReadOnlyList<ConsultationDto>>;

public class GetConsultationsByCycleHandler : IRequestHandler<GetConsultationsByCycleQuery, IReadOnlyList<ConsultationDto>>
{
    private readonly IConsultationRepository _consultationRepo;
    public GetConsultationsByCycleHandler(IConsultationRepository consultationRepo) => _consultationRepo = consultationRepo;

    public async Task<IReadOnlyList<ConsultationDto>> Handle(GetConsultationsByCycleQuery request, CancellationToken ct)
    {
        var consultations = await _consultationRepo.GetByCycleIdAsync(request.CycleId, ct);
        return consultations.Select(ConsultationDto.FromEntity).ToList();
    }
}

// ==================== SEARCH ====================
public record SearchConsultationsQuery(string? Query, string? Status, string? Type, DateTime? FromDate, DateTime? ToDate, int Page = 1, int PageSize = 20)
    : IRequest<(IReadOnlyList<ConsultationDto> Items, int Total)>;

public class SearchConsultationsHandler : IRequestHandler<SearchConsultationsQuery, (IReadOnlyList<ConsultationDto> Items, int Total)>
{
    private readonly IConsultationRepository _consultationRepo;
    public SearchConsultationsHandler(IConsultationRepository consultationRepo) => _consultationRepo = consultationRepo;

    public async Task<(IReadOnlyList<ConsultationDto> Items, int Total)> Handle(SearchConsultationsQuery request, CancellationToken ct)
    {
        var (items, total) = await _consultationRepo.SearchAsync(request.Query, request.Status, request.Type, request.FromDate, request.ToDate, request.Page, request.PageSize, ct);
        var dtos = items.Select(ConsultationDto.FromEntity).ToList();
        return (dtos, total);
    }
}
