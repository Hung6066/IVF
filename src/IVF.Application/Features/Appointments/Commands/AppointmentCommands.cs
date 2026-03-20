using FluentValidation;
using IVF.Application.Common;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Domain.Enums;
using MediatR;

namespace IVF.Application.Features.Appointments.Commands;

// === DTOs ===

public record AppointmentDto(
    Guid Id,
    Guid PatientId,
    string PatientName,
    string PatientCode,
    Guid? CycleId,
    Guid? DoctorId,
    string? DoctorName,
    DateTime ScheduledAt,
    int DurationMinutes,
    string Type,
    string Status,
    string? Notes,
    string? RoomNumber,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

// === Commands ===

public record CreateAppointmentCommand(
    Guid PatientId,
    DateTime ScheduledAt,
    AppointmentType Type,
    Guid? CycleId = null,
    Guid? DoctorId = null,
    int DurationMinutes = 30,
    string? Notes = null,
    string? RoomNumber = null) : IRequest<Result<AppointmentDto>>;

public class CreateAppointmentValidator : AbstractValidator<CreateAppointmentCommand>
{
    public CreateAppointmentValidator()
    {
        RuleFor(x => x.PatientId).NotEmpty().WithMessage("Vui lòng chọn bệnh nhân");
        RuleFor(x => x.ScheduledAt).GreaterThan(DateTime.UtcNow.AddMinutes(-5)).WithMessage("Thời gian hẹn phải ở tương lai");
        RuleFor(x => x.DurationMinutes).InclusiveBetween(5, 480).WithMessage("Thời lượng phải từ 5 đến 480 phút");
        RuleFor(x => x.Type).IsInEnum().WithMessage("Loại lịch hẹn không hợp lệ");
    }
}

public class CreateAppointmentHandler(
    IAppointmentRepository repo,
    IDoctorRepository docRepo,
    INotificationService notifService,
    IUnitOfWork uow) : IRequestHandler<CreateAppointmentCommand, Result<AppointmentDto>>
{
    public async Task<Result<AppointmentDto>> Handle(CreateAppointmentCommand request, CancellationToken ct)
    {
        if (request.DoctorId.HasValue)
        {
            var hasConflict = await repo.HasConflictAsync(request.DoctorId.Value, request.ScheduledAt, request.DurationMinutes, ct: ct);
            if (hasConflict)
                return Result<AppointmentDto>.Failure("Bác sĩ có xung đột lịch vào thời gian này");
        }

        var apt = Appointment.Create(
            request.PatientId,
            request.ScheduledAt,
            request.Type,
            request.CycleId,
            request.DoctorId,
            request.DurationMinutes,
            request.Notes,
            request.RoomNumber);

        await repo.AddAsync(apt, ct);
        await uow.SaveChangesAsync(ct);

        if (request.DoctorId.HasValue)
        {
            var doctor = await docRepo.GetByIdAsync(request.DoctorId.Value, ct);
            if (doctor != null)
                await notifService.SendAppointmentReminderAsync(doctor.UserId, apt.Id, request.ScheduledAt);
        }

        var created = await repo.GetByIdAsync(apt.Id, ct);
        return Result<AppointmentDto>.Success(AppointmentMapper.MapToDto(created!));
    }
}

// --- Confirm ---

public record ConfirmAppointmentCommand(Guid AppointmentId) : IRequest<Result<AppointmentDto>>;

public class ConfirmAppointmentHandler(IAppointmentRepository repo, IUnitOfWork uow)
    : IRequestHandler<ConfirmAppointmentCommand, Result<AppointmentDto>>
{
    public async Task<Result<AppointmentDto>> Handle(ConfirmAppointmentCommand request, CancellationToken ct)
    {
        var apt = await repo.GetByIdAsync(request.AppointmentId, ct);
        if (apt is null) return Result<AppointmentDto>.Failure("Không tìm thấy lịch hẹn");
        apt.Confirm();
        await uow.SaveChangesAsync(ct);
        return Result<AppointmentDto>.Success(AppointmentMapper.MapToDto(apt));
    }
}

// --- CheckIn ---

public record CheckInAppointmentCommand(Guid AppointmentId) : IRequest<Result<AppointmentDto>>;

public class CheckInAppointmentHandler(IAppointmentRepository repo, IUnitOfWork uow)
    : IRequestHandler<CheckInAppointmentCommand, Result<AppointmentDto>>
{
    public async Task<Result<AppointmentDto>> Handle(CheckInAppointmentCommand request, CancellationToken ct)
    {
        var apt = await repo.GetByIdAsync(request.AppointmentId, ct);
        if (apt is null) return Result<AppointmentDto>.Failure("Không tìm thấy lịch hẹn");
        apt.CheckIn();
        await uow.SaveChangesAsync(ct);
        return Result<AppointmentDto>.Success(AppointmentMapper.MapToDto(apt));
    }
}

// --- Complete ---

public record CompleteAppointmentCommand(Guid AppointmentId) : IRequest<Result<AppointmentDto>>;

public class CompleteAppointmentHandler(IAppointmentRepository repo, IUnitOfWork uow)
    : IRequestHandler<CompleteAppointmentCommand, Result<AppointmentDto>>
{
    public async Task<Result<AppointmentDto>> Handle(CompleteAppointmentCommand request, CancellationToken ct)
    {
        var apt = await repo.GetByIdAsync(request.AppointmentId, ct);
        if (apt is null) return Result<AppointmentDto>.Failure("Không tìm thấy lịch hẹn");
        apt.Complete();
        await uow.SaveChangesAsync(ct);
        return Result<AppointmentDto>.Success(AppointmentMapper.MapToDto(apt));
    }
}

// --- Cancel ---

public record CancelAppointmentCommand(Guid AppointmentId, string? Reason = null) : IRequest<Result<AppointmentDto>>;

public class CancelAppointmentValidator : AbstractValidator<CancelAppointmentCommand>
{
    public CancelAppointmentValidator()
    {
        RuleFor(x => x.AppointmentId).NotEmpty();
    }
}

public class CancelAppointmentHandler(IAppointmentRepository repo, IUnitOfWork uow)
    : IRequestHandler<CancelAppointmentCommand, Result<AppointmentDto>>
{
    public async Task<Result<AppointmentDto>> Handle(CancelAppointmentCommand request, CancellationToken ct)
    {
        var apt = await repo.GetByIdAsync(request.AppointmentId, ct);
        if (apt is null) return Result<AppointmentDto>.Failure("Không tìm thấy lịch hẹn");
        apt.Cancel(request.Reason);
        await uow.SaveChangesAsync(ct);
        return Result<AppointmentDto>.Success(AppointmentMapper.MapToDto(apt));
    }
}

// --- NoShow ---

public record NoShowAppointmentCommand(Guid AppointmentId) : IRequest<Result<AppointmentDto>>;

public class NoShowAppointmentHandler(IAppointmentRepository repo, IUnitOfWork uow)
    : IRequestHandler<NoShowAppointmentCommand, Result<AppointmentDto>>
{
    public async Task<Result<AppointmentDto>> Handle(NoShowAppointmentCommand request, CancellationToken ct)
    {
        var apt = await repo.GetByIdAsync(request.AppointmentId, ct);
        if (apt is null) return Result<AppointmentDto>.Failure("Không tìm thấy lịch hẹn");
        apt.NoShow();
        await uow.SaveChangesAsync(ct);
        return Result<AppointmentDto>.Success(AppointmentMapper.MapToDto(apt));
    }
}

// --- Reschedule ---

public record RescheduleAppointmentCommand(Guid AppointmentId, DateTime NewDateTime) : IRequest<Result<AppointmentDto>>;

public class RescheduleAppointmentValidator : AbstractValidator<RescheduleAppointmentCommand>
{
    public RescheduleAppointmentValidator()
    {
        RuleFor(x => x.AppointmentId).NotEmpty();
        RuleFor(x => x.NewDateTime).GreaterThan(DateTime.UtcNow.AddMinutes(-5)).WithMessage("Thời gian mới phải ở tương lai");
    }
}

public class RescheduleAppointmentHandler(IAppointmentRepository repo, IUnitOfWork uow)
    : IRequestHandler<RescheduleAppointmentCommand, Result<AppointmentDto>>
{
    public async Task<Result<AppointmentDto>> Handle(RescheduleAppointmentCommand request, CancellationToken ct)
    {
        var apt = await repo.GetByIdAsync(request.AppointmentId, ct);
        if (apt is null) return Result<AppointmentDto>.Failure("Không tìm thấy lịch hẹn");

        if (apt.DoctorId.HasValue)
        {
            var hasConflict = await repo.HasConflictAsync(apt.DoctorId.Value, request.NewDateTime, apt.DurationMinutes, apt.Id, ct);
            if (hasConflict)
                return Result<AppointmentDto>.Failure("Bác sĩ có xung đột lịch vào thời gian này");
        }

        apt.Reschedule(request.NewDateTime);
        await uow.SaveChangesAsync(ct);
        return Result<AppointmentDto>.Success(AppointmentMapper.MapToDto(apt));
    }
}

// === Mapping Helper ===

internal static class AppointmentMapper
{
    public static AppointmentDto MapToDto(Appointment a) => new(
        a.Id,
        a.PatientId,
        a.Patient?.FullName ?? "",
        a.Patient?.PatientCode ?? "",
        a.CycleId,
        a.DoctorId,
        a.Doctor?.User?.FullName ?? "",
        a.ScheduledAt,
        a.DurationMinutes,
        a.Type.ToString(),
        a.Status.ToString(),
        a.Notes,
        a.RoomNumber,
        a.CreatedAt,
        a.UpdatedAt);
}
