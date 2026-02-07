using FluentValidation;
using IVF.Application.Common;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Domain.Enums;
using MediatR;

namespace IVF.Application.Features.Andrology.Commands;

// ==================== CREATE SPERM WASHING ====================
public record CreateSpermWashingCommand(
    Guid CycleId,
    Guid PatientId,
    string Method,
    DateTime WashDate,
    string? Notes,
    decimal? PreWashConcentration,
    decimal? PostWashConcentration,
    decimal? PostWashMotility
) : IRequest<Result<SpermWashingDto>>;

public class CreateSpermWashingValidator : AbstractValidator<CreateSpermWashingCommand>
{
    public CreateSpermWashingValidator()
    {
        RuleFor(x => x.CycleId).NotEmpty();
        RuleFor(x => x.PatientId).NotEmpty();
        RuleFor(x => x.Method).NotEmpty();
        RuleFor(x => x.WashDate).NotEmpty();
    }
}

public class CreateSpermWashingHandler : IRequestHandler<CreateSpermWashingCommand, Result<SpermWashingDto>>
{
    private readonly ISpermWashingRepository _washingRepo;
    private readonly IPatientRepository _patientRepo;
    private readonly ITreatmentCycleRepository _cycleRepo;
    private readonly IUnitOfWork _unitOfWork;

    public CreateSpermWashingHandler(
        ISpermWashingRepository washingRepo, 
        IPatientRepository patientRepo,
        ITreatmentCycleRepository cycleRepo,
        IUnitOfWork unitOfWork)
    {
        _washingRepo = washingRepo;
        _patientRepo = patientRepo;
        _cycleRepo = cycleRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<SpermWashingDto>> Handle(CreateSpermWashingCommand request, CancellationToken ct)
    {
        var patient = await _patientRepo.GetByIdAsync(request.PatientId, ct);
        if (patient == null) return Result<SpermWashingDto>.Failure("Patient not found");

        var cycle = await _cycleRepo.GetByIdAsync(request.CycleId, ct);
        if (cycle == null) return Result<SpermWashingDto>.Failure("Cycle not found");

        var washing = SpermWashing.Create(request.CycleId, request.PatientId, request.Method, request.WashDate);
        
        // If results are provided during creation, record them
        if (!string.IsNullOrEmpty(request.Notes) || request.PreWashConcentration != null || request.PostWashConcentration != null || request.PostWashMotility != null)
        {
            washing.UpdateResult(request.PreWashConcentration, request.PostWashConcentration, request.PostWashMotility, request.Notes);
        }
        
        await _washingRepo.AddAsync(washing, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        var dto = new SpermWashingDto(
            washing.Id, washing.CycleId, washing.PatientId, washing.Method,
            washing.PreWashConcentration, washing.PostWashConcentration, washing.PostWashMotility,
            washing.WashDate, washing.Status.ToString(), washing.Notes,
            patient.FullName, patient.PatientCode, cycle.CycleCode
        );

        return Result<SpermWashingDto>.Success(dto);
    }
}

// ==================== SEARCH SPERM WASHINGS ====================
// Move Query here or to Queries file? Queries file is cleaner but I'll put it here for now with DTO.
// Actually, let's put Query in AndrologyQueries.cs later or use this DTO.
// ==================== UPDATE SPERM WASHING ====================
public record UpdateSpermWashingCommand(
    Guid Id,
    string? Notes,
    decimal? PreWashConcentration,
    decimal? PostWashConcentration,
    decimal? PostWashMotility
) : IRequest<Result<SpermWashingDto>>;

public class UpdateSpermWashingHandler : IRequestHandler<UpdateSpermWashingCommand, Result<SpermWashingDto>>
{
    private readonly ISpermWashingRepository _washingRepo;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateSpermWashingHandler(ISpermWashingRepository washingRepo, IUnitOfWork unitOfWork)
    {
        _washingRepo = washingRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<SpermWashingDto>> Handle(UpdateSpermWashingCommand request, CancellationToken ct)
    {
        var washing = await _washingRepo.GetByIdAsync(request.Id, ct);
        if (washing == null)
            return Result<SpermWashingDto>.Failure("Washing not found");

        washing.UpdateResult(request.PreWashConcentration, request.PostWashConcentration, request.PostWashMotility, request.Notes);
        
        await _washingRepo.UpdateAsync(washing, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<SpermWashingDto>.Success(SpermWashingDto.FromEntity(washing));
    }
}

public record SpermWashingDto(
    Guid Id,
    Guid CycleId,
    Guid PatientId,
    string Method,
    decimal? PreWashConcentration,
    decimal? PostWashConcentration,
    decimal? PostWashMotility,
    DateTime WashDate,
    string? Status,
    string? Notes,
    // Include Patient/Cycle info for UI?
    string PatientName,
    string PatientCode,
    string CycleCode
)
{
    public static SpermWashingDto FromEntity(SpermWashing w) => new(
        w.Id, w.CycleId, w.PatientId, w.Method,
        w.PreWashConcentration, w.PostWashConcentration, w.PostWashMotility,
        w.WashDate, w.Status.ToString(), w.Notes,
        w.Patient?.FullName ?? "", w.Patient?.PatientCode ?? "", w.Cycle?.CycleCode ?? ""
    );
}

// ==================== SEARCH QUERY for SpermWashing (placed here to use DTO easily) ====================
// Or should go to AndrologyQueries.cs. Let's put it here to keep washing stuff together? 
// No, separation is better. But I'll put it in AndrologyQueries.cs if I can access DTO.
// SemenAnalysisDto is in Commands, used in Queries. So SpermWashingDto in Commands commands can be used in Queries.
