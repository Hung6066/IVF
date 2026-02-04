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
    DateTime FertilizationDate
) : IRequest<Result<EmbryoDto>>;

public class CreateEmbryoHandler : IRequestHandler<CreateEmbryoCommand, Result<EmbryoDto>>
{
    private readonly IEmbryoRepository _embryoRepo;
    private readonly ITreatmentCycleRepository _cycleRepo;
    private readonly IUnitOfWork _unitOfWork;

    public CreateEmbryoHandler(IEmbryoRepository embryoRepo, ITreatmentCycleRepository cycleRepo, IUnitOfWork unitOfWork)
    {
        _embryoRepo = embryoRepo;
        _cycleRepo = cycleRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<EmbryoDto>> Handle(CreateEmbryoCommand request, CancellationToken ct)
    {
        var cycle = await _cycleRepo.GetByIdAsync(request.CycleId, ct);
        if (cycle == null)
            return Result<EmbryoDto>.Failure("Cycle not found");

        var number = await _embryoRepo.GetNextNumberForCycleAsync(request.CycleId, ct);
        var embryo = Embryo.Create(request.CycleId, number, request.FertilizationDate);
        
        await _embryoRepo.AddAsync(embryo, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<EmbryoDto>.Success(EmbryoDto.FromEntity(embryo));
    }
}

// ==================== UPDATE GRADE ====================
public record UpdateEmbryoGradeCommand(
    Guid EmbryoId,
    EmbryoGrade Grade,
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
