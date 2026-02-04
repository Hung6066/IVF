using FluentValidation;
using IVF.Application.Common;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Domain.Enums;
using MediatR;

namespace IVF.Application.Features.Andrology.Commands;

// ==================== CREATE SEMEN ANALYSIS ====================
public record CreateSemenAnalysisCommand(
    Guid PatientId,
    DateTime AnalysisDate,
    AnalysisType AnalysisType,
    Guid? CycleId,
    Guid? PerformedByUserId
) : IRequest<Result<SemenAnalysisDto>>;

public class CreateSemenAnalysisValidator : AbstractValidator<CreateSemenAnalysisCommand>
{
    public CreateSemenAnalysisValidator()
    {
        RuleFor(x => x.PatientId).NotEmpty();
        RuleFor(x => x.AnalysisDate).NotEmpty();
    }
}

public class CreateSemenAnalysisHandler : IRequestHandler<CreateSemenAnalysisCommand, Result<SemenAnalysisDto>>
{
    private readonly ISemenAnalysisRepository _analysisRepo;
    private readonly IPatientRepository _patientRepo;
    private readonly IUnitOfWork _unitOfWork;

    public CreateSemenAnalysisHandler(ISemenAnalysisRepository analysisRepo, IPatientRepository patientRepo, IUnitOfWork unitOfWork)
    {
        _analysisRepo = analysisRepo;
        _patientRepo = patientRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<SemenAnalysisDto>> Handle(CreateSemenAnalysisCommand request, CancellationToken ct)
    {
        var patient = await _patientRepo.GetByIdAsync(request.PatientId, ct);
        if (patient == null)
            return Result<SemenAnalysisDto>.Failure("Patient not found");

        var analysis = SemenAnalysis.Create(request.PatientId, request.AnalysisDate, request.AnalysisType, request.CycleId, request.PerformedByUserId);
        await _analysisRepo.AddAsync(analysis, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<SemenAnalysisDto>.Success(SemenAnalysisDto.FromEntity(analysis));
    }
}

// ==================== RECORD MACROSCOPIC ====================
public record RecordMacroscopicCommand(
    Guid AnalysisId,
    decimal? Volume,
    string? Appearance,
    string? Liquefaction,
    decimal? Ph
) : IRequest<Result<SemenAnalysisDto>>;

public class RecordMacroscopicHandler : IRequestHandler<RecordMacroscopicCommand, Result<SemenAnalysisDto>>
{
    private readonly ISemenAnalysisRepository _analysisRepo;
    private readonly IUnitOfWork _unitOfWork;

    public RecordMacroscopicHandler(ISemenAnalysisRepository analysisRepo, IUnitOfWork unitOfWork)
    {
        _analysisRepo = analysisRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<SemenAnalysisDto>> Handle(RecordMacroscopicCommand request, CancellationToken ct)
    {
        var analysis = await _analysisRepo.GetByIdAsync(request.AnalysisId, ct);
        if (analysis == null)
            return Result<SemenAnalysisDto>.Failure("Analysis not found");

        analysis.RecordMacroscopic(request.Volume, request.Appearance, request.Liquefaction, request.Ph);
        await _analysisRepo.UpdateAsync(analysis, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<SemenAnalysisDto>.Success(SemenAnalysisDto.FromEntity(analysis));
    }
}

// ==================== RECORD MICROSCOPIC ====================
public record RecordMicroscopicCommand(
    Guid AnalysisId,
    decimal? Concentration,
    decimal? TotalCount,
    decimal? ProgressiveMotility,
    decimal? NonProgressiveMotility,
    decimal? Immotile,
    decimal? NormalMorphology,
    decimal? Vitality
) : IRequest<Result<SemenAnalysisDto>>;

public class RecordMicroscopicHandler : IRequestHandler<RecordMicroscopicCommand, Result<SemenAnalysisDto>>
{
    private readonly ISemenAnalysisRepository _analysisRepo;
    private readonly IUnitOfWork _unitOfWork;

    public RecordMicroscopicHandler(ISemenAnalysisRepository analysisRepo, IUnitOfWork unitOfWork)
    {
        _analysisRepo = analysisRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<SemenAnalysisDto>> Handle(RecordMicroscopicCommand request, CancellationToken ct)
    {
        var analysis = await _analysisRepo.GetByIdAsync(request.AnalysisId, ct);
        if (analysis == null)
            return Result<SemenAnalysisDto>.Failure("Analysis not found");

        analysis.RecordMicroscopic(request.Concentration, request.TotalCount, request.ProgressiveMotility,
            request.NonProgressiveMotility, request.Immotile, request.NormalMorphology, request.Vitality);
        await _analysisRepo.UpdateAsync(analysis, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<SemenAnalysisDto>.Success(SemenAnalysisDto.FromEntity(analysis));
    }
}

// ==================== DTO ====================
public record SemenAnalysisDto(
    Guid Id,
    Guid PatientId,
    Guid? CycleId,
    DateTime AnalysisDate,
    string AnalysisType,
    decimal? Volume,
    decimal? Concentration,
    decimal? TotalCount,
    decimal? ProgressiveMotility,
    decimal? NormalMorphology,
    decimal? Vitality,
    DateTime CreatedAt
)
{
    public static SemenAnalysisDto FromEntity(SemenAnalysis a) => new(
        a.Id, a.PatientId, a.CycleId, a.AnalysisDate, a.AnalysisType.ToString(),
        a.Volume, a.Concentration, a.TotalCount, a.ProgressiveMotility, a.NormalMorphology, a.Vitality, a.CreatedAt
    );
}
