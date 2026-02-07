using IVF.Application.Common.Interfaces;
using IVF.Application.Features.Andrology.Commands;
using MediatR;
using IVF.Domain.Entities;
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

// ==================== SEARCH ANALYSES ====================
public record SearchSemenAnalysesQuery(string? Query, DateTime? FromDate, DateTime? ToDate, string? Status, int Page = 1, int PageSize = 20) 
    : IRequest<(IReadOnlyList<SemenAnalysisDto> Items, int Total)>;

public class SearchSemenAnalysesHandler : IRequestHandler<SearchSemenAnalysesQuery, (IReadOnlyList<SemenAnalysisDto> Items, int Total)>
{
    private readonly ISemenAnalysisRepository _analysisRepo;
    public SearchSemenAnalysesHandler(ISemenAnalysisRepository analysisRepo) => _analysisRepo = analysisRepo;

    public async Task<(IReadOnlyList<SemenAnalysisDto> Items, int Total)> Handle(SearchSemenAnalysesQuery request, CancellationToken ct)
    {
        var (items, total) = await _analysisRepo.SearchAsync(request.Query, request.FromDate, request.ToDate, request.Status, request.Page, request.PageSize, ct);
        var dtos = items.Select(SemenAnalysisDto.FromEntity).ToList();
        return (dtos, total);
    }
}

// ==================== SEARCH WASHINGS ====================
public record SearchSpermWashingsQuery(string? Method, DateTime? FromDate, DateTime? ToDate, int Page = 1, int PageSize = 20) 
    : IRequest<(IReadOnlyList<SpermWashingDto> Items, int Total)>;

public class SearchSpermWashingsHandler : IRequestHandler<SearchSpermWashingsQuery, (IReadOnlyList<SpermWashingDto> Items, int Total)>
{
    private readonly ISpermWashingRepository _washingRepo;
    public SearchSpermWashingsHandler(ISpermWashingRepository washingRepo) => _washingRepo = washingRepo;

    public async Task<(IReadOnlyList<SpermWashingDto> Items, int Total)> Handle(SearchSpermWashingsQuery request, CancellationToken ct)
    {
        var (items, total) = await _washingRepo.SearchAsync(request.Method, request.FromDate, request.ToDate, request.Page, request.PageSize, ct);
        var dtos = items.Select(SpermWashingDto.FromEntity).ToList();
        return (dtos, total);
    }
}

// ==================== STATISTICS ====================
public record AndrologyStatisticsDto(int TodayAnalyses, int TodayWashings, int PendingAnalyses, decimal AvgConcentration, Dictionary<string, int> ConcentrationDistribution);

public record GetAndrologyStatisticsQuery : IRequest<AndrologyStatisticsDto>;

public class GetAndrologyStatisticsHandler : IRequestHandler<GetAndrologyStatisticsQuery, AndrologyStatisticsDto>
{
    private readonly ISemenAnalysisRepository _analysisRepo;
    private readonly ISpermWashingRepository _washingRepo;

    public GetAndrologyStatisticsHandler(ISemenAnalysisRepository analysisRepo, ISpermWashingRepository washingRepo)
    {
        _analysisRepo = analysisRepo;
        _washingRepo = washingRepo;
    }

    public async Task<AndrologyStatisticsDto> Handle(GetAndrologyStatisticsQuery request, CancellationToken ct)
    {
        var today = DateTime.UtcNow.Date;
        var todayAnalyses = await _analysisRepo.GetCountByDateAsync(today, ct);
        var todayWashings = await _washingRepo.GetCountByDateAsync(today, ct);
        
        var (_, pendingCount) = await _analysisRepo.SearchAsync(null, null, null, "Pending", 1, 1, ct);
        
        var avgConc = await _analysisRepo.GetAverageConcentrationAsync(ct);
        var dist = await _analysisRepo.GetConcentrationDistributionAsync(ct);

        return new AndrologyStatisticsDto(todayAnalyses, todayWashings, pendingCount, avgConc ?? 0, dist);
    }
}
