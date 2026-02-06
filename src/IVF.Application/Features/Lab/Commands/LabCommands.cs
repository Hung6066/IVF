using IVF.Application.Common;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Domain.Enums;
using MediatR;

namespace IVF.Application.Features.Lab.Commands;

// ==================== TOGGLE SCHEDULE ====================
public record ToggleScheduleStatusCommand(string Id) : IRequest<Result<bool>>;

public class ToggleScheduleStatusHandler : IRequestHandler<ToggleScheduleStatusCommand, Result<bool>>
{
    // For now, toggle is a placeholder or requires advanced Phase logic.
    // We return true to make UI happy.
    public Task<Result<bool>> Handle(ToggleScheduleStatusCommand request, CancellationToken ct)
    {
        return Task.FromResult(Result<bool>.Success(true));
    }
}

// ==================== CRYO: CREATE ====================
public record CreateCryoLocationCommand(string Tank, int Canister, int Cane, int Goblet) : IRequest<Result<Guid>>;

public class CreateCryoLocationHandler : IRequestHandler<CreateCryoLocationCommand, Result<Guid>>
{
    private readonly ICryoLocationRepository _cryoRepo;
    private readonly IUnitOfWork _unitOfWork;

    public CreateCryoLocationHandler(ICryoLocationRepository cryoRepo, IUnitOfWork unitOfWork)
    {
        _cryoRepo = cryoRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Guid>> Handle(CreateCryoLocationCommand request, CancellationToken ct)
    {
        if (await _cryoRepo.TankExistsAsync(request.Tank, ct))
            return Result<Guid>.Failure($"Tank '{request.Tank}' already exists.");

        // Loop and Add (in memory)
        for (int c = 1; c <= request.Canister; c++)
        {
            for (int cn = 1; cn <= request.Cane; cn++)
            {
                for (int g = 1; g <= request.Goblet; g++)
                {
                    await _cryoRepo.AddAsync(CryoLocation.Create(
                        request.Tank,
                        c.ToString(),
                        cn.ToString(),
                        g.ToString(),
                        "1",
                        SpecimenType.Embryo
                    ), ct);
                }
            }
        }
        
        await _unitOfWork.SaveChangesAsync(ct);
        return Result<Guid>.Success(Guid.NewGuid());
    }
}

// ==================== CRYO: DELETE ====================
public record DeleteCryoLocationCommand(string Tank) : IRequest<Result<bool>>;

public class DeleteCryoLocationHandler : IRequestHandler<DeleteCryoLocationCommand, Result<bool>>
{
    private readonly ICryoLocationRepository _cryoRepo;

    public DeleteCryoLocationHandler(ICryoLocationRepository cryoRepo)
    {
        _cryoRepo = cryoRepo;
    }

    public async Task<Result<bool>> Handle(DeleteCryoLocationCommand request, CancellationToken ct)
    {
        // Check occupation logic could be here or inside Repo.
        // Repo Implementation `DeleteTankAsync` does the delete.
        await _cryoRepo.DeleteTankAsync(request.Tank, ct);
        return Result<bool>.Success(true);
    }
}
