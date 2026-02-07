using FluentValidation;
using IVF.Application.Common;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Domain.Enums;
using MediatR;

namespace IVF.Application.Features.Embryos.Commands;

// ==================== CREATE EMBRYO ====================
public record CreateEmbryoCommand(
    Guid CycleId,
    DateTime FertilizationDate,
    string? Grade = null,
    EmbryoDay Day = EmbryoDay.D1,
    EmbryoStatus Status = EmbryoStatus.Developing,
    string? Location = null // Added Location
) : IRequest<Result<EmbryoDto>>;

public class CreateEmbryoHandler : IRequestHandler<CreateEmbryoCommand, Result<EmbryoDto>>
{
    private readonly IEmbryoRepository _embryoRepo;
    private readonly ITreatmentCycleRepository _cycleRepo;
    private readonly ICryoLocationRepository _cryoRepo; // Added CryoRepo
    private readonly IUnitOfWork _unitOfWork;

    public CreateEmbryoHandler(IEmbryoRepository embryoRepo, ITreatmentCycleRepository cycleRepo, ICryoLocationRepository cryoRepo, IUnitOfWork unitOfWork)
    {
        _embryoRepo = embryoRepo;
        _cycleRepo = cycleRepo;
        _cryoRepo = cryoRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<EmbryoDto>> Handle(CreateEmbryoCommand request, CancellationToken ct)
    {
        var cycle = await _cycleRepo.GetByIdAsync(request.CycleId, ct);
        if (cycle == null)
            return Result<EmbryoDto>.Failure("Cycle not found");

        var number = await _embryoRepo.GetNextNumberForCycleAsync(request.CycleId, ct);
        var embryo = Embryo.Create(
            request.CycleId, 
            number, 
            request.FertilizationDate,
            request.Grade,
            request.Day,
            request.Status
        );

        // Handle Frozen Status with Location
        if (request.Status == EmbryoStatus.Frozen && !string.IsNullOrEmpty(request.Location))
        {
            // Find a free spot in the requested Tank
            // Logic: Get all available locations, filter by Tank name, pick first.
            // This is a simple strategy. A better one might be specific slot selection, but user only gave Tank name.
            var availableLocations = await _cryoRepo.GetAvailableAsync(ct);
            var spot = availableLocations.FirstOrDefault(l => l.Tank == request.Location);

            if (spot != null)
            {
                spot.Occupy(); // Mark as used
                embryo.Freeze(spot.Id); // Link to embryo
                await _cryoRepo.UpdateAsync(spot, ct);
            }
            else
            {
                // If no spot found in that tank, what to do?
                // For now, maybe just log or result in failure? 
                // Or create the embryo without location but logged warning?
                // User expects it to be in "Tank K1". If full, return error.
                return Result<EmbryoDto>.Failure($"No available space in {request.Location}");
            }
        }
        
        await _embryoRepo.AddAsync(embryo, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<EmbryoDto>.Success(EmbryoDto.FromEntity(embryo));
    }
}

// ==================== UPDATE EMBRYO ====================
public record UpdateEmbryoCommand(
    Guid Id,
    Guid CycleId,
    DateTime FertilizationDate,
    string? Grade = null,
    EmbryoDay Day = EmbryoDay.D1,
    EmbryoStatus Status = EmbryoStatus.Developing,
    string? Location = null
) : IRequest<Result<EmbryoDto>>;

public class UpdateEmbryoHandler : IRequestHandler<UpdateEmbryoCommand, Result<EmbryoDto>>
{
    private readonly IEmbryoRepository _embryoRepo;
    private readonly ICryoLocationRepository _cryoRepo;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateEmbryoHandler(IEmbryoRepository embryoRepo, ICryoLocationRepository cryoRepo, IUnitOfWork unitOfWork)
    {
        _embryoRepo = embryoRepo;
        _cryoRepo = cryoRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<EmbryoDto>> Handle(UpdateEmbryoCommand request, CancellationToken ct)
    {
        var embryo = await _embryoRepo.GetByIdAsync(request.Id, ct);
        if (embryo == null)
            return Result<EmbryoDto>.Failure("Embryo not found");

        var oldStatus = embryo.Status;
        var oldLocationId = embryo.CryoLocationId;

        // Update basic properties
        embryo.UpdateGrade(request.Grade, request.Day);

        // Handle status and location changes
        if (request.Status == EmbryoStatus.Frozen && oldStatus != EmbryoStatus.Frozen)
        {
            // Changing TO Frozen - need to find and occupy a location
            if (!string.IsNullOrEmpty(request.Location))
            {
                var availableLocations = await _cryoRepo.GetAvailableAsync(ct);
                var spot = availableLocations.FirstOrDefault(l => l.Tank == request.Location);

                if (spot != null)
                {
                    spot.Occupy();
                    embryo.Freeze(spot.Id);
                    await _cryoRepo.UpdateAsync(spot, ct);
                }
                else
                {
                    return Result<EmbryoDto>.Failure($"No available space in {request.Location}");
                }
            }
        }
        else if (request.Status != EmbryoStatus.Frozen && oldStatus == EmbryoStatus.Frozen)
        {
            // Changing FROM Frozen - need to release the old location
            if (oldLocationId.HasValue)
            {
                var oldLocation = await _cryoRepo.GetByIdAsync(oldLocationId.Value, ct);
                if (oldLocation != null)
                {
                    oldLocation.Release();
                    await _cryoRepo.UpdateAsync(oldLocation, ct);
                }
            }
            embryo.Thaw();
        }
        else if (request.Status == EmbryoStatus.Frozen && oldStatus == EmbryoStatus.Frozen)
        {
            // Still Frozen but maybe changing location or assigning one for the first time
            if (!string.IsNullOrEmpty(request.Location))
            {
                if (oldLocationId.HasValue)
                {
                    // Has existing location - check if changing tank
                    var oldLocation = await _cryoRepo.GetByIdAsync(oldLocationId.Value, ct);
                    if (oldLocation != null && oldLocation.Tank != request.Location)
                    {
                        // Release old location
                        oldLocation.Release();
                        await _cryoRepo.UpdateAsync(oldLocation, ct);

                        // Find new location
                        var availableLocations = await _cryoRepo.GetAvailableAsync(ct);
                        var newSpot = availableLocations.FirstOrDefault(l => l.Tank == request.Location);

                        if (newSpot != null)
                        {
                            newSpot.Occupy();
                            embryo.Freeze(newSpot.Id);
                            await _cryoRepo.UpdateAsync(newSpot, ct);
                        }
                        else
                        {
                            return Result<EmbryoDto>.Failure($"No available space in {request.Location}");
                        }
                    }
                }
                else
                {
                    // No existing location but Frozen - need to assign one
                    var availableLocations = await _cryoRepo.GetAvailableAsync(ct);
                    var spot = availableLocations.FirstOrDefault(l => l.Tank == request.Location);

                    if (spot != null)
                    {
                        spot.Occupy();
                        embryo.Freeze(spot.Id);
                        await _cryoRepo.UpdateAsync(spot, ct);
                    }
                    else
                    {
                        return Result<EmbryoDto>.Failure($"No available space in {request.Location}");
                    }
                }
            }
        }

        await _embryoRepo.UpdateAsync(embryo, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<EmbryoDto>.Success(EmbryoDto.FromEntity(embryo));
    }
}

// ==================== UPDATE GRADE ====================
public record UpdateEmbryoGradeCommand(
    Guid EmbryoId,
    string Grade,
    EmbryoDay Day
) : IRequest<Result<EmbryoDto>>;

public class UpdateEmbryoGradeHandler : IRequestHandler<UpdateEmbryoGradeCommand, Result<EmbryoDto>>
{
    private readonly IEmbryoRepository _embryoRepo;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateEmbryoGradeHandler(IEmbryoRepository embryoRepo, IUnitOfWork unitOfWork)
    {
        _embryoRepo = embryoRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<EmbryoDto>> Handle(UpdateEmbryoGradeCommand request, CancellationToken ct)
    {
        var embryo = await _embryoRepo.GetByIdAsync(request.EmbryoId, ct);
        if (embryo == null)
            return Result<EmbryoDto>.Failure("Embryo not found");

        embryo.UpdateGrade(request.Grade, request.Day);
        await _embryoRepo.UpdateAsync(embryo, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<EmbryoDto>.Success(EmbryoDto.FromEntity(embryo));
    }
}

// ==================== TRANSFER EMBRYO ====================
public record TransferEmbryoCommand(Guid EmbryoId) : IRequest<Result>;

public class TransferEmbryoHandler : IRequestHandler<TransferEmbryoCommand, Result>
{
    private readonly IEmbryoRepository _embryoRepo;
    private readonly IUnitOfWork _unitOfWork;

    public TransferEmbryoHandler(IEmbryoRepository embryoRepo, IUnitOfWork unitOfWork)
    {
        _embryoRepo = embryoRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(TransferEmbryoCommand request, CancellationToken ct)
    {
        var embryo = await _embryoRepo.GetByIdAsync(request.EmbryoId, ct);
        if (embryo == null)
            return Result.Failure("Embryo not found");

        embryo.Transfer();
        await _embryoRepo.UpdateAsync(embryo, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }
}

// ==================== FREEZE EMBRYO ====================
public record FreezeEmbryoCommand(Guid EmbryoId, Guid CryoLocationId) : IRequest<Result<EmbryoDto>>;

public class FreezeEmbryoHandler : IRequestHandler<FreezeEmbryoCommand, Result<EmbryoDto>>
{
    private readonly IEmbryoRepository _embryoRepo;
    private readonly ICryoLocationRepository _cryoRepo;
    private readonly IUnitOfWork _unitOfWork;

    public FreezeEmbryoHandler(IEmbryoRepository embryoRepo, ICryoLocationRepository cryoRepo, IUnitOfWork unitOfWork)
    {
        _embryoRepo = embryoRepo;
        _cryoRepo = cryoRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<EmbryoDto>> Handle(FreezeEmbryoCommand request, CancellationToken ct)
    {
        var embryo = await _embryoRepo.GetByIdAsync(request.EmbryoId, ct);
        if (embryo == null)
            return Result<EmbryoDto>.Failure("Embryo not found");

        var location = await _cryoRepo.GetByIdAsync(request.CryoLocationId, ct);
        if (location == null)
            return Result<EmbryoDto>.Failure("Cryo location not found");

        if (location.IsOccupied)
            return Result<EmbryoDto>.Failure("Cryo location is already occupied");

        embryo.Freeze(request.CryoLocationId);
        location.Occupy();
        
        await _embryoRepo.UpdateAsync(embryo, ct);
        await _cryoRepo.UpdateAsync(location, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<EmbryoDto>.Success(EmbryoDto.FromEntity(embryo));
    }
}

// ==================== THAW EMBRYO ====================
public record ThawEmbryoCommand(Guid EmbryoId) : IRequest<Result<EmbryoDto>>;

public class ThawEmbryoHandler : IRequestHandler<ThawEmbryoCommand, Result<EmbryoDto>>
{
    private readonly IEmbryoRepository _embryoRepo;
    private readonly ICryoLocationRepository _cryoRepo;
    private readonly IUnitOfWork _unitOfWork;

    public ThawEmbryoHandler(IEmbryoRepository embryoRepo, ICryoLocationRepository cryoRepo, IUnitOfWork unitOfWork)
    {
        _embryoRepo = embryoRepo;
        _cryoRepo = cryoRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<EmbryoDto>> Handle(ThawEmbryoCommand request, CancellationToken ct)
    {
        var embryo = await _embryoRepo.GetByIdAsync(request.EmbryoId, ct);
        if (embryo == null)
            return Result<EmbryoDto>.Failure("Embryo not found");

        if (embryo.CryoLocationId.HasValue)
        {
            var location = await _cryoRepo.GetByIdAsync(embryo.CryoLocationId.Value, ct);
            if (location != null)
            {
                location.Release();
                await _cryoRepo.UpdateAsync(location, ct);
            }
        }

        embryo.Thaw();
        await _embryoRepo.UpdateAsync(embryo, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<EmbryoDto>.Success(EmbryoDto.FromEntity(embryo));
    }
}

// ==================== DELETE EMBRYO ====================
public record DeleteEmbryoCommand(Guid EmbryoId) : IRequest<Result>;

public class DeleteEmbryoHandler : IRequestHandler<DeleteEmbryoCommand, Result>
{
    private readonly IEmbryoRepository _embryoRepo;
    private readonly ICryoLocationRepository _cryoRepo;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteEmbryoHandler(IEmbryoRepository embryoRepo, ICryoLocationRepository cryoRepo, IUnitOfWork unitOfWork)
    {
        _embryoRepo = embryoRepo;
        _cryoRepo = cryoRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(DeleteEmbryoCommand request, CancellationToken ct)
    {
        var embryo = await _embryoRepo.GetByIdAsync(request.EmbryoId, ct);
        if (embryo == null)
            return Result.Failure("Embryo not found");

        // Release cryo location if frozen
        if (embryo.CryoLocationId.HasValue)
        {
            var location = await _cryoRepo.GetByIdAsync(embryo.CryoLocationId.Value, ct);
            if (location != null)
            {
                location.Release();
                await _cryoRepo.UpdateAsync(location, ct);
            }
        }

        await _embryoRepo.DeleteAsync(embryo, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }
}

// ==================== DTO ====================
public record EmbryoDto(
    Guid Id,
    Guid CycleId,
    int EmbryoNumber,
    DateTime FertilizationDate,
    string? Grade,
    string Day,
    string Status,
    Guid? CryoLocationId,
    DateTime? FreezeDate,
    DateTime? ThawDate,
    DateTime CreatedAt
)
{
    public static EmbryoDto FromEntity(Embryo e) => new(
        e.Id, e.CycleId, e.EmbryoNumber, e.FertilizationDate,
        e.Grade?.ToString(), e.Day.ToString(), e.Status.ToString(),
        e.CryoLocationId, e.FreezeDate, e.ThawDate, e.CreatedAt
    );
}
