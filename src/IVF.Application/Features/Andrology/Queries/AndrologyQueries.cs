using IVF.Application.Common.Interfaces;
using IVF.Application.Features.Andrology.Commands;
using MediatR;

namespace IVF.Application.Features.Andrology.Queries;

// ==================== GET BY PATIENT ====================
public record GetAnalysesByPatientQuery(Guid PatientId) : IRequest<IReadOnlyList<SemenAnalysisDto>>;

public class GetAnalysesByPatientHandler : IRequestHandler<GetAnalysesByPatientQuery, IReadOnlyList<SemenAnalysisDto>>
{
    private readonly ISemenAnalysisRepository _analysisRepo;

    public GetAnalysesByPatientHandler(ISemenAnalysisRepository analysisRepo) => _analysisRepo = analysisRepo;

    public async Task<IReadOnlyList<SemenAnalysisDto>> Handle(GetAnalysesByPatientQuery request, CancellationToken ct)
    {
        var analyses = await _analysisRepo.GetByPatientIdAsync(request.PatientId, ct);
        return analyses.Select(SemenAnalysisDto.FromEntity).ToList();
    }
}

// ==================== GET BY CYCLE ====================
public record GetAnalysesByCycleQuery(Guid CycleId) : IRequest<IReadOnlyList<SemenAnalysisDto>>;

public class GetAnalysesByCycleHandler : IRequestHandler<GetAnalysesByCycleQuery, IReadOnlyList<SemenAnalysisDto>>
{
    private readonly ISemenAnalysisRepository _analysisRepo;

    public GetAnalysesByCycleHandler(ISemenAnalysisRepository analysisRepo) => _analysisRepo = analysisRepo;

    public async Task<IReadOnlyList<SemenAnalysisDto>> Handle(GetAnalysesByCycleQuery request, CancellationToken ct)
    {
        var analyses = await _analysisRepo.GetByCycleIdAsync(request.CycleId, ct);
        return analyses.Select(SemenAnalysisDto.FromEntity).ToList();
    }
}
