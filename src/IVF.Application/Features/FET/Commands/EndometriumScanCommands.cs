using IVF.Application.Common;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using MediatR;

namespace IVF.Application.Features.FET.Commands;

// ==================== ENDOMETRIUM SCAN DTOs ====================

public record EndometriumScanDto(
    Guid Id,
    Guid CycleId,
    Guid? FetProtocolId,
    DateTime ScanDate,
    int CycleDay,
    decimal ThicknessMm,
    string? Pattern,
    decimal? LengthMm,
    decimal? WidthMm,
    bool PolypsOrMyomata,
    string? FluidInCavity,
    decimal? E2Level,
    decimal? LhLevel,
    decimal? P4Level,
    bool IsAdequate,
    string? Recommendation,
    string? Notes,
    DateTime CreatedAt
);

// ==================== CREATE ENDOMETRIUM SCAN ====================

public record CreateEndometriumScanCommand(
    Guid CycleId,
    Guid? FetProtocolId,
    DateTime ScanDate,
    int CycleDay,
    decimal ThicknessMm,
    string? Pattern,
    decimal? LengthMm,
    decimal? WidthMm,
    bool PolypsOrMyomata,
    string? FluidInCavity,
    decimal? E2Level,
    decimal? LhLevel,
    decimal? P4Level,
    string? Recommendation,
    string? Notes,
    Guid? DoneByUserId
) : IRequest<Result<EndometriumScanDto>>;

public class CreateEndometriumScanHandler : IRequestHandler<CreateEndometriumScanCommand, Result<EndometriumScanDto>>
{
    private readonly IEndometriumScanRepository _repo;
    private readonly IUnitOfWork _unitOfWork;

    public CreateEndometriumScanHandler(IEndometriumScanRepository repo, IUnitOfWork unitOfWork)
    {
        _repo = repo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<EndometriumScanDto>> Handle(CreateEndometriumScanCommand r, CancellationToken ct)
    {
        var scan = EndometriumScan.Create(r.CycleId, r.ScanDate, r.CycleDay, r.ThicknessMm, r.Pattern, r.FetProtocolId, r.DoneByUserId);
        scan.UpdateMeasurements(r.ThicknessMm, r.Pattern, r.LengthMm, r.WidthMm, r.PolypsOrMyomata, r.FluidInCavity);
        if (r.E2Level.HasValue || r.LhLevel.HasValue || r.P4Level.HasValue)
            scan.RecordHormones(r.E2Level, r.LhLevel, r.P4Level);
        if (!string.IsNullOrEmpty(r.Recommendation))
            scan.AddRecommendation(r.Recommendation, r.Notes);

        await _repo.AddAsync(scan, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<EndometriumScanDto>.Success(MapToDto(scan));
    }

    internal static EndometriumScanDto MapToDto(EndometriumScan s) => new(
        s.Id, s.CycleId, s.FetProtocolId, s.ScanDate, s.CycleDay,
        s.ThicknessMm, s.Pattern, s.LengthMm, s.WidthMm, s.PolypsOrMyomata,
        s.FluidInCavity, s.E2Level, s.LhLevel, s.P4Level,
        s.IsAdequate, s.Recommendation, s.Notes, s.CreatedAt
    );
}

// ==================== GET SCANS BY CYCLE ====================

public record GetEndometriumScansByCycleQuery(Guid CycleId) : IRequest<IReadOnlyList<EndometriumScanDto>>;

public class GetEndometriumScansByCycleHandler : IRequestHandler<GetEndometriumScansByCycleQuery, IReadOnlyList<EndometriumScanDto>>
{
    private readonly IEndometriumScanRepository _repo;

    public GetEndometriumScansByCycleHandler(IEndometriumScanRepository repo) => _repo = repo;

    public async Task<IReadOnlyList<EndometriumScanDto>> Handle(GetEndometriumScansByCycleQuery r, CancellationToken ct)
    {
        var scans = await _repo.GetByCycleIdAsync(r.CycleId, ct);
        return scans.Select(CreateEndometriumScanHandler.MapToDto).ToList();
    }
}
