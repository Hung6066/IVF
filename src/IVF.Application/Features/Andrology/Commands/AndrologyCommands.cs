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
    Guid? PerformedByUserId,
    // Macroscopic (Optional)
    decimal? Volume,
    string? Appearance,
    string? Liquefaction,
    decimal? Ph,
    // Microscopic (Optional)
    decimal? Concentration,
    decimal? TotalCount,
    decimal? ProgressiveMotility,
    decimal? NonProgressiveMotility,
    decimal? Immotile,
    decimal? NormalMorphology,
    decimal? Vitality
) : IRequest<Result<SemenAnalysisDto>>;

public class CreateSemenAnalysisValidator : AbstractValidator<CreateSemenAnalysisCommand>
{
    public CreateSemenAnalysisValidator()
    {
        RuleFor(x => x.PatientId).NotEmpty().WithMessage("Patient ID is required");
        RuleFor(x => x.AnalysisDate).NotEmpty().WithMessage("Analysis Date is required");
        RuleFor(x => x.AnalysisType).IsInEnum().WithMessage("Invalid Analysis Type");
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
        // 1. Validate Patient
        var patient = await _patientRepo.GetByIdAsync(request.PatientId, ct);
        if (patient == null)
            return Result<SemenAnalysisDto>.Failure($"Patient with ID {request.PatientId} not found");

        // 2. Create Entity
        var analysis = SemenAnalysis.Create(
            request.PatientId, 
            request.AnalysisDate, 
            request.AnalysisType, 
            request.CycleId, 
            request.PerformedByUserId);
        
        // 3. Record results (if any provided)
        // Check Macroscopic
        if (request.Volume.HasValue || !string.IsNullOrWhiteSpace(request.Appearance) || 
            !string.IsNullOrWhiteSpace(request.Liquefaction) || request.Ph.HasValue)
        {
            analysis.RecordMacroscopic(request.Volume, request.Appearance, request.Liquefaction, request.Ph);
        }

        // Check Microscopic
        if (request.Concentration.HasValue || request.TotalCount.HasValue || request.ProgressiveMotility.HasValue ||
            request.NonProgressiveMotility.HasValue || request.Immotile.HasValue || 
            request.NormalMorphology.HasValue || request.Vitality.HasValue)
        {
            analysis.RecordMicroscopic(request.Concentration, request.TotalCount, request.ProgressiveMotility,
                request.NonProgressiveMotility, request.Immotile, request.NormalMorphology, request.Vitality);
        }

        try 
        {
            await _analysisRepo.AddAsync(analysis, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // Log error here if logger available
            return Result<SemenAnalysisDto>.Failure($"Failed to save analysis: {ex.Message}");
        }

        // 4. Return DTO
        var dto = new SemenAnalysisDto(
            analysis.Id, analysis.PatientId, analysis.CycleId, analysis.AnalysisDate, analysis.AnalysisType.ToString(),
            analysis.Volume, analysis.Concentration, analysis.TotalCount, analysis.ProgressiveMotility, analysis.NormalMorphology, analysis.Vitality, analysis.CreatedAt,
            patient.FullName, patient.PatientCode,
            analysis.Concentration == null ? "Pending" : "Completed"
        );

        return Result<SemenAnalysisDto>.Success(dto);
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
    string? Appearance,
    string? Liquefaction,
    decimal? Ph,
    decimal? Concentration,
    decimal? TotalCount,
    decimal? ProgressiveMotility,
    decimal? NonProgressiveMotility,
    decimal? Immotile,
    decimal? NormalMorphology,
    decimal? Vitality,
    DateTime CreatedAt,
    string PatientName,
    string PatientCode,
    string Status
)
{
    public static SemenAnalysisDto FromEntity(SemenAnalysis a) => new(
        a.Id, a.PatientId, a.CycleId, a.AnalysisDate, a.AnalysisType.ToString(),
        a.Volume, a.Appearance, a.Liquefaction, a.Ph,
        a.Concentration, a.TotalCount, a.ProgressiveMotility, 
        a.NonProgressiveMotility, a.Immotile, 
        a.NormalMorphology, a.Vitality, a.CreatedAt,
        a.Patient?.FullName ?? "", a.Patient?.PatientCode ?? "",
        a.Concentration == null ? "Pending" : "Completed"
    );
}
