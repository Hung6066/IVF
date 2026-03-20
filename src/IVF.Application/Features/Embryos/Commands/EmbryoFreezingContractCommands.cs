using IVF.Application.Common;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using MediatR;

namespace IVF.Application.Features.Embryos.Commands;

// ==================== EMBRYO FREEZING CONTRACT COMMANDS ====================

public record EmbryoFreezingContractDto(
    Guid Id,
    Guid CycleId,
    Guid PatientId,
    string ContractNumber,
    DateTime ContractDate,
    DateTime StorageStartDate,
    DateTime StorageEndDate,
    int StorageDurationMonths,
    decimal AnnualFee,
    decimal TotalFeesPaid,
    DateTime? LastPaymentDate,
    DateTime? NextPaymentDue,
    string Status,
    bool PatientSigned,
    DateTime? PatientSignedAt,
    string? Notes,
    DateTime CreatedAt
);

// ==================== CREATE ====================

public record CreateEmbryoFreezingContractCommand(
    Guid CycleId,
    Guid PatientId,
    DateTime ContractDate,
    DateTime StorageStartDate,
    int StorageDurationMonths,
    decimal AnnualFee,
    string? Notes
) : IRequest<Result<EmbryoFreezingContractDto>>;

public class CreateEmbryoFreezingContractHandler : IRequestHandler<CreateEmbryoFreezingContractCommand, Result<EmbryoFreezingContractDto>>
{
    private readonly IEmbryoFreezingContractRepository _repo;
    private readonly IUnitOfWork _unitOfWork;

    public CreateEmbryoFreezingContractHandler(IEmbryoFreezingContractRepository repo, IUnitOfWork unitOfWork)
    {
        _repo = repo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<EmbryoFreezingContractDto>> Handle(CreateEmbryoFreezingContractCommand r, CancellationToken ct)
    {
        var contractNo = await _repo.GenerateContractNumberAsync(ct);
        var contract = EmbryoFreezingContract.Create(
            r.CycleId, r.PatientId, contractNo, r.ContractDate,
            r.StorageStartDate, r.StorageDurationMonths, r.AnnualFee);

        await _repo.AddAsync(contract, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<EmbryoFreezingContractDto>.Success(MapToDto(contract));
    }

    internal static EmbryoFreezingContractDto MapToDto(EmbryoFreezingContract c) => new(
        c.Id, c.CycleId, c.PatientId, c.ContractNumber, c.ContractDate,
        c.StorageStartDate, c.StorageEndDate, c.StorageDurationMonths,
        c.AnnualFee, c.TotalFeesPaid, c.LastPaymentDate, c.NextPaymentDue,
        c.Status, c.PatientSigned, c.PatientSignedAt, c.Notes, c.CreatedAt
    );
}

// ==================== GET BY CYCLE ====================

public record GetEmbryoFreezingContractsByCycleQuery(Guid CycleId) : IRequest<IReadOnlyList<EmbryoFreezingContractDto>>;

public class GetEmbryoFreezingContractsByCycleHandler : IRequestHandler<GetEmbryoFreezingContractsByCycleQuery, IReadOnlyList<EmbryoFreezingContractDto>>
{
    private readonly IEmbryoFreezingContractRepository _repo;

    public GetEmbryoFreezingContractsByCycleHandler(IEmbryoFreezingContractRepository repo) => _repo = repo;

    public async Task<IReadOnlyList<EmbryoFreezingContractDto>> Handle(GetEmbryoFreezingContractsByCycleQuery r, CancellationToken ct)
    {
        var contracts = await _repo.GetByCycleIdAsync(r.CycleId, ct);
        return contracts.Select(CreateEmbryoFreezingContractHandler.MapToDto).ToList();
    }
}

// ==================== RECORD PAYMENT ====================

public record RecordContractPaymentCommand(Guid ContractId, decimal Amount) : IRequest<Result<EmbryoFreezingContractDto>>;

public class RecordContractPaymentHandler : IRequestHandler<RecordContractPaymentCommand, Result<EmbryoFreezingContractDto>>
{
    private readonly IEmbryoFreezingContractRepository _repo;
    private readonly IUnitOfWork _unitOfWork;

    public RecordContractPaymentHandler(IEmbryoFreezingContractRepository repo, IUnitOfWork unitOfWork)
    {
        _repo = repo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<EmbryoFreezingContractDto>> Handle(RecordContractPaymentCommand r, CancellationToken ct)
    {
        var contract = await _repo.GetByIdAsync(r.ContractId, ct);
        if (contract == null) return Result<EmbryoFreezingContractDto>.Failure("Contract not found");

        contract.RecordPayment(r.Amount);
        await _repo.UpdateAsync(contract, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<EmbryoFreezingContractDto>.Success(CreateEmbryoFreezingContractHandler.MapToDto(contract));
    }
}
