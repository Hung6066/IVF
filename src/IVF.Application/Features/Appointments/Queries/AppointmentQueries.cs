using IVF.Application.Common;
using IVF.Application.Common.Interfaces;
using IVF.Application.Features.Appointments.Commands;
using IVF.Domain.Enums;
using MediatR;

namespace IVF.Application.Features.Appointments.Queries;

// === Get by ID ===

public record GetAppointmentByIdQuery(Guid Id) : IRequest<Result<AppointmentDto>>;

public class GetAppointmentByIdHandler(IAppointmentRepository repo)
    : IRequestHandler<GetAppointmentByIdQuery, Result<AppointmentDto>>
{
    public async Task<Result<AppointmentDto>> Handle(GetAppointmentByIdQuery request, CancellationToken ct)
    {
        var apt = await repo.GetByIdAsync(request.Id, ct);
        if (apt is null) return Result<AppointmentDto>.Failure("Không tìm thấy lịch hẹn");
        return Result<AppointmentDto>.Success(AppointmentMapper.MapToDto(apt));
    }
}

// === Get by Patient ===

public record GetAppointmentsByPatientQuery(Guid PatientId) : IRequest<Result<IReadOnlyList<AppointmentDto>>>;

public class GetAppointmentsByPatientHandler(IAppointmentRepository repo)
    : IRequestHandler<GetAppointmentsByPatientQuery, Result<IReadOnlyList<AppointmentDto>>>
{
    public async Task<Result<IReadOnlyList<AppointmentDto>>> Handle(GetAppointmentsByPatientQuery request, CancellationToken ct)
    {
        var items = await repo.GetByPatientAsync(request.PatientId, ct);
        return Result<IReadOnlyList<AppointmentDto>>.Success(items.Select(AppointmentMapper.MapToDto).ToList());
    }
}

// === Get by Doctor ===

public record GetAppointmentsByDoctorQuery(Guid DoctorId, DateTime? Date = null) : IRequest<Result<IReadOnlyList<AppointmentDto>>>;

public class GetAppointmentsByDoctorHandler(IAppointmentRepository repo)
    : IRequestHandler<GetAppointmentsByDoctorQuery, Result<IReadOnlyList<AppointmentDto>>>
{
    public async Task<Result<IReadOnlyList<AppointmentDto>>> Handle(GetAppointmentsByDoctorQuery request, CancellationToken ct)
    {
        var date = request.Date ?? DateTime.UtcNow.Date;
        var items = await repo.GetByDoctorAsync(request.DoctorId, date, ct);
        return Result<IReadOnlyList<AppointmentDto>>.Success(items.Select(AppointmentMapper.MapToDto).ToList());
    }
}

// === Get Today ===

public record GetTodayAppointmentsQuery : IRequest<Result<IReadOnlyList<AppointmentDto>>>;

public class GetTodayAppointmentsHandler(IAppointmentRepository repo)
    : IRequestHandler<GetTodayAppointmentsQuery, Result<IReadOnlyList<AppointmentDto>>>
{
    public async Task<Result<IReadOnlyList<AppointmentDto>>> Handle(GetTodayAppointmentsQuery request, CancellationToken ct)
    {
        var items = await repo.GetTodayAppointmentsAsync(ct);
        return Result<IReadOnlyList<AppointmentDto>>.Success(items.Select(AppointmentMapper.MapToDto).ToList());
    }
}

// === Get by Date Range ===

public record GetAppointmentsByDateRangeQuery(DateTime? Start = null, DateTime? End = null) : IRequest<Result<IReadOnlyList<AppointmentDto>>>;

public class GetAppointmentsByDateRangeHandler(IAppointmentRepository repo)
    : IRequestHandler<GetAppointmentsByDateRangeQuery, Result<IReadOnlyList<AppointmentDto>>>
{
    public async Task<Result<IReadOnlyList<AppointmentDto>>> Handle(GetAppointmentsByDateRangeQuery request, CancellationToken ct)
    {
        var start = request.Start ?? DateTime.UtcNow.Date;
        var end = request.End ?? start.AddDays(7);
        var items = await repo.GetByDateRangeAsync(start, end, ct);
        return Result<IReadOnlyList<AppointmentDto>>.Success(items.Select(AppointmentMapper.MapToDto).ToList());
    }
}

// === Get Upcoming ===

public record GetUpcomingAppointmentsQuery(Guid? DoctorId = null, int Days = 7) : IRequest<Result<IReadOnlyList<AppointmentDto>>>;

public class GetUpcomingAppointmentsHandler(IAppointmentRepository repo)
    : IRequestHandler<GetUpcomingAppointmentsQuery, Result<IReadOnlyList<AppointmentDto>>>
{
    public async Task<Result<IReadOnlyList<AppointmentDto>>> Handle(GetUpcomingAppointmentsQuery request, CancellationToken ct)
    {
        var items = await repo.GetUpcomingAsync(request.DoctorId, request.Days, ct);
        return Result<IReadOnlyList<AppointmentDto>>.Success(items.Select(AppointmentMapper.MapToDto).ToList());
    }
}
