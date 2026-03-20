using IVF.Application.Common;
using IVF.Application.Common.Interfaces;
using IVF.Application.Features.Pregnancy.Commands;
using MediatR;

using ICycleRepository = IVF.Application.Common.Interfaces.ITreatmentCycleRepository;

namespace IVF.Application.Features.Pregnancy.Queries;

// === Get Pregnancy by Cycle ===

public record GetPregnancyByCycleQuery(Guid CycleId) : IRequest<Result<PregnancyDto>>;

public class GetPregnancyByCycleHandler(ICyclePhaseDataRepository phaseRepo, ICycleRepository cycleRepo)
    : IRequestHandler<GetPregnancyByCycleQuery, Result<PregnancyDto>>
{
    public async Task<Result<PregnancyDto>> Handle(GetPregnancyByCycleQuery request, CancellationToken ct)
    {
        var cycle = await cycleRepo.GetByIdAsync(request.CycleId, ct);
        if (cycle is null) return Result<PregnancyDto>.Failure("Không tìm thấy chu kỳ");

        var pregnancy = await phaseRepo.GetPregnancyByCycleIdAsync(request.CycleId, ct);
        if (pregnancy is null)
            return Result<PregnancyDto>.Success(new PregnancyDto(
                request.CycleId, null, null, false, null, null, null, null, "Chưa có kết quả"));

        return Result<PregnancyDto>.Success(MapToDto(pregnancy));
    }

    private static PregnancyDto MapToDto(IVF.Domain.Entities.PregnancyData e) => new(
        e.CycleId, e.BetaHcg, e.BetaHcgDate, e.IsPregnant,
        e.GestationalSacs, e.FetalHeartbeats, e.DueDate, e.Notes,
        e.IsPregnant ? (e.GestationalSacs.HasValue ? "Có thai" : "Dương tính") : "Âm tính");
}// === Get Beta HCG Results (history) ===

public record BetaHcgResultDto(decimal? Value, DateTime? TestDate, bool IsPregnant, string Status);

public record GetBetaHcgResultsQuery(Guid CycleId) : IRequest<Result<List<BetaHcgResultDto>>>;

public class GetBetaHcgResultsHandler(ICyclePhaseDataRepository phaseRepo)
    : IRequestHandler<GetBetaHcgResultsQuery, Result<List<BetaHcgResultDto>>>
{
    public async Task<Result<List<BetaHcgResultDto>>> Handle(GetBetaHcgResultsQuery request, CancellationToken ct)
    {
        var pregnancy = await phaseRepo.GetPregnancyByCycleIdAsync(request.CycleId, ct);
        if (pregnancy is null) return Result<List<BetaHcgResultDto>>.Success(new List<BetaHcgResultDto>());

        var results = new List<BetaHcgResultDto>
        {
            new(
                pregnancy.BetaHcg,
                pregnancy.BetaHcgDate,
                pregnancy.IsPregnant,
                pregnancy.IsPregnant ? "DƯƠNG TÍNH" : "ÂM TÍNH")
        };
        return Result<List<BetaHcgResultDto>>.Success(results);
    }
}

// === Get Pregnancy Follow-Up Plan ===

public record FollowUpItemDto(DateTime ScheduledDate, string VisitType, string Description, bool IsCompleted);

public record GetPregnancyFollowUpQuery(Guid CycleId) : IRequest<Result<List<FollowUpItemDto>>>;

public class GetPregnancyFollowUpHandler(ICyclePhaseDataRepository phaseRepo)
    : IRequestHandler<GetPregnancyFollowUpQuery, Result<List<FollowUpItemDto>>>
{
    public async Task<Result<List<FollowUpItemDto>>> Handle(GetPregnancyFollowUpQuery request, CancellationToken ct)
    {
        var pregnancy = await phaseRepo.GetPregnancyByCycleIdAsync(request.CycleId, ct);
        if (pregnancy is null || !pregnancy.IsPregnant)
            return Result<List<FollowUpItemDto>>.Success(new List<FollowUpItemDto>());

        var followUp = new List<FollowUpItemDto>();
        if (pregnancy.BetaHcgDate.HasValue)
        {
            var baseDate = pregnancy.BetaHcgDate.Value;
            followUp.Add(new FollowUpItemDto(
                baseDate.AddDays(2),
                "Beta HCG lần 2",
                "Kiểm tra xu hướng tăng HCG",
                false));
            followUp.Add(new FollowUpItemDto(
                baseDate.AddDays(35),
                "SA 7 tuần",
                "SA xác nhận số thai, tim thai, ngày dự sinh",
                false));
        }

        return Result<List<FollowUpItemDto>>.Success(followUp);
    }
}
