using FluentValidation;
using IVF.Application.Common;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using MediatR;

namespace IVF.Application.Features.FET.Commands;

// === DTO ===

public record FetProtocolDto(
    Guid Id,
    Guid CycleId,
    string PrepType,
    DateTime? StartDate,
    int CycleDay,
    string? EstrogenDrug,
    string? EstrogenDose,
    DateTime? EstrogenStartDate,
    string? ProgesteroneDrug,
    string? ProgesteroneDose,
    DateTime? ProgesteroneStartDate,
    decimal? EndometriumThickness,
    string? EndometriumPattern,
    DateTime? EndometriumCheckDate,
    int EmbryosToThaw,
    int EmbryosSurvived,
    DateTime? ThawDate,
    string? EmbryoGrade,
    int EmbryoAge,
    DateTime? PlannedTransferDate,
    string? Notes,
    string Status,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

// === Create ===

public record CreateFetProtocolCommand(
    Guid CycleId,
    string PrepType,
    DateTime? StartDate = null,
    int CycleDay = 1,
    string? Notes = null) : IRequest<Result<FetProtocolDto>>;

public class CreateFetProtocolValidator : AbstractValidator<CreateFetProtocolCommand>
{
    public CreateFetProtocolValidator()
    {
        RuleFor(x => x.CycleId).NotEmpty().WithMessage("Vui lòng chọn chu kỳ");
        RuleFor(x => x.PrepType).NotEmpty().WithMessage("Vui lòng chọn phác đồ chuẩn bị");
    }
}

public class CreateFetProtocolHandler(IFetProtocolRepository repo, IUnitOfWork uow)
    : IRequestHandler<CreateFetProtocolCommand, Result<FetProtocolDto>>
{
    public async Task<Result<FetProtocolDto>> Handle(CreateFetProtocolCommand request, CancellationToken ct)
    {
        var existing = await repo.GetByCycleIdAsync(request.CycleId, ct);
        if (existing is not null)
            return Result<FetProtocolDto>.Failure("Chu kỳ này đã có FET protocol");

        var protocol = FetProtocol.Create(request.CycleId, request.PrepType, request.StartDate, request.CycleDay, request.Notes);
        await repo.AddAsync(protocol, ct);
        await uow.SaveChangesAsync(ct);

        var created = await repo.GetByIdAsync(protocol.Id, ct);
        return Result<FetProtocolDto>.Success(FetProtocolMapper.MapToDto(created!));
    }
}

// === Update Hormone Therapy ===

public record UpdateHormoneTherapyCommand(
    Guid FetProtocolId,
    string? EstrogenDrug,
    string? EstrogenDose,
    DateTime? EstrogenStartDate,
    string? ProgesteroneDrug,
    string? ProgesteroneDose,
    DateTime? ProgesteroneStartDate) : IRequest<Result<FetProtocolDto>>;

public class UpdateHormoneTherapyHandler(IFetProtocolRepository repo, IUnitOfWork uow)
    : IRequestHandler<UpdateHormoneTherapyCommand, Result<FetProtocolDto>>
{
    public async Task<Result<FetProtocolDto>> Handle(UpdateHormoneTherapyCommand request, CancellationToken ct)
    {
        var protocol = await repo.GetByIdAsync(request.FetProtocolId, ct);
        if (protocol is null) return Result<FetProtocolDto>.Failure("Không tìm thấy FET protocol");

        protocol.UpdateHormoneTherapy(
            request.EstrogenDrug, request.EstrogenDose, request.EstrogenStartDate,
            request.ProgesteroneDrug, request.ProgesteroneDose, request.ProgesteroneStartDate);
        await uow.SaveChangesAsync(ct);
        return Result<FetProtocolDto>.Success(FetProtocolMapper.MapToDto(protocol));
    }
}

// === Record Endometrium Check ===

public record RecordEndometriumCheckCommand(
    Guid FetProtocolId,
    decimal Thickness,
    string? Pattern,
    DateTime CheckDate) : IRequest<Result<FetProtocolDto>>;

public class RecordEndometriumCheckValidator : AbstractValidator<RecordEndometriumCheckCommand>
{
    public RecordEndometriumCheckValidator()
    {
        RuleFor(x => x.FetProtocolId).NotEmpty();
        RuleFor(x => x.Thickness).GreaterThan(0).WithMessage("Độ dày NMTC phải lớn hơn 0");
    }
}

public class RecordEndometriumCheckHandler(IFetProtocolRepository repo, IUnitOfWork uow)
    : IRequestHandler<RecordEndometriumCheckCommand, Result<FetProtocolDto>>
{
    public async Task<Result<FetProtocolDto>> Handle(RecordEndometriumCheckCommand request, CancellationToken ct)
    {
        var protocol = await repo.GetByIdAsync(request.FetProtocolId, ct);
        if (protocol is null) return Result<FetProtocolDto>.Failure("Không tìm thấy FET protocol");

        protocol.RecordEndometriumCheck(request.Thickness, request.Pattern, request.CheckDate);
        await uow.SaveChangesAsync(ct);
        return Result<FetProtocolDto>.Success(FetProtocolMapper.MapToDto(protocol));
    }
}

// === Record Thawing ===

public record RecordThawingCommand(
    Guid FetProtocolId,
    int EmbryosToThaw,
    int EmbryosSurvived,
    DateTime ThawDate,
    string? EmbryoGrade,
    int EmbryoAge) : IRequest<Result<FetProtocolDto>>;

public class RecordThawingValidator : AbstractValidator<RecordThawingCommand>
{
    public RecordThawingValidator()
    {
        RuleFor(x => x.FetProtocolId).NotEmpty();
        RuleFor(x => x.EmbryosToThaw).GreaterThan(0).WithMessage("Số phôi rã phải lớn hơn 0");
        RuleFor(x => x.EmbryosSurvived).GreaterThanOrEqualTo(0).LessThanOrEqualTo(x => x.EmbryosToThaw).WithMessage("Số phôi sống phải <= số phôi rã");
        RuleFor(x => x.EmbryoAge).InclusiveBetween(1, 7).WithMessage("Tuổi phôi phải từ 1-7 ngày");
    }
}

public class RecordThawingHandler(IFetProtocolRepository repo, IUnitOfWork uow)
    : IRequestHandler<RecordThawingCommand, Result<FetProtocolDto>>
{
    public async Task<Result<FetProtocolDto>> Handle(RecordThawingCommand request, CancellationToken ct)
    {
        var protocol = await repo.GetByIdAsync(request.FetProtocolId, ct);
        if (protocol is null) return Result<FetProtocolDto>.Failure("Không tìm thấy FET protocol");

        protocol.RecordThawing(request.EmbryosToThaw, request.EmbryosSurvived, request.ThawDate, request.EmbryoGrade, request.EmbryoAge);
        await uow.SaveChangesAsync(ct);
        return Result<FetProtocolDto>.Success(FetProtocolMapper.MapToDto(protocol));
    }
}

// === Schedule Transfer ===

public record ScheduleTransferCommand(Guid FetProtocolId, DateTime TransferDate) : IRequest<Result<FetProtocolDto>>;

public class ScheduleTransferHandler(IFetProtocolRepository repo, IUnitOfWork uow)
    : IRequestHandler<ScheduleTransferCommand, Result<FetProtocolDto>>
{
    public async Task<Result<FetProtocolDto>> Handle(ScheduleTransferCommand request, CancellationToken ct)
    {
        var protocol = await repo.GetByIdAsync(request.FetProtocolId, ct);
        if (protocol is null) return Result<FetProtocolDto>.Failure("Không tìm thấy FET protocol");

        protocol.ScheduleTransfer(request.TransferDate);
        await uow.SaveChangesAsync(ct);
        return Result<FetProtocolDto>.Success(FetProtocolMapper.MapToDto(protocol));
    }
}

// === Mark Transferred ===

public record MarkFetTransferredCommand(Guid FetProtocolId) : IRequest<Result<FetProtocolDto>>;

public class MarkFetTransferredHandler(IFetProtocolRepository repo, IUnitOfWork uow)
    : IRequestHandler<MarkFetTransferredCommand, Result<FetProtocolDto>>
{
    public async Task<Result<FetProtocolDto>> Handle(MarkFetTransferredCommand request, CancellationToken ct)
    {
        var protocol = await repo.GetByIdAsync(request.FetProtocolId, ct);
        if (protocol is null) return Result<FetProtocolDto>.Failure("Không tìm thấy FET protocol");

        protocol.MarkTransferred();
        await uow.SaveChangesAsync(ct);
        return Result<FetProtocolDto>.Success(FetProtocolMapper.MapToDto(protocol));
    }
}

// === Cancel ===

public record CancelFetProtocolCommand(Guid FetProtocolId, string? Reason = null) : IRequest<Result<FetProtocolDto>>;

public class CancelFetProtocolHandler(IFetProtocolRepository repo, IUnitOfWork uow)
    : IRequestHandler<CancelFetProtocolCommand, Result<FetProtocolDto>>
{
    public async Task<Result<FetProtocolDto>> Handle(CancelFetProtocolCommand request, CancellationToken ct)
    {
        var protocol = await repo.GetByIdAsync(request.FetProtocolId, ct);
        if (protocol is null) return Result<FetProtocolDto>.Failure("Không tìm thấy FET protocol");

        protocol.Cancel(request.Reason);
        await uow.SaveChangesAsync(ct);
        return Result<FetProtocolDto>.Success(FetProtocolMapper.MapToDto(protocol));
    }
}

// === Mapping ===

internal static class FetProtocolMapper
{
    public static FetProtocolDto MapToDto(FetProtocol p) => new(
        p.Id, p.CycleId, p.PrepType, p.StartDate, p.CycleDay,
        p.EstrogenDrug, p.EstrogenDose, p.EstrogenStartDate,
        p.ProgesteroneDrug, p.ProgesteroneDose, p.ProgesteroneStartDate,
        p.EndometriumThickness, p.EndometriumPattern, p.EndometriumCheckDate,
        p.EmbryosToThaw, p.EmbryosSurvived, p.ThawDate, p.EmbryoGrade, p.EmbryoAge,
        p.PlannedTransferDate, p.Notes, p.Status, p.CreatedAt, p.UpdatedAt);
}
