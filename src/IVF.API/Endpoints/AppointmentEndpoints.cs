using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Domain.Enums;

namespace IVF.API.Endpoints;

public static class AppointmentEndpoints
{
    public static void MapAppointmentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/appointments").WithTags("Appointments").RequireAuthorization();

        // Get today's appointments
        group.MapGet("/today", async (IAppointmentRepository repo) =>
            Results.Ok(await repo.GetTodayAppointmentsAsync()));

        // Get appointments by date range
        group.MapGet("/", async (IAppointmentRepository repo, DateTime? start, DateTime? end) =>
        {
            var startDate = start ?? DateTime.UtcNow.Date;
            var endDate = end ?? startDate.AddDays(7);
            return Results.Ok(await repo.GetByDateRangeAsync(startDate, endDate));
        });

        // Get upcoming appointments
        group.MapGet("/upcoming", async (IAppointmentRepository repo, Guid? doctorId, int days = 7) =>
            Results.Ok(await repo.GetUpcomingAsync(doctorId, days)));

        // Get appointment by ID
        group.MapGet("/{id:guid}", async (Guid id, IAppointmentRepository repo) =>
        {
            var apt = await repo.GetByIdAsync(id);
            return apt is null ? Results.NotFound() : Results.Ok(apt);
        });

        // Get appointments by patient
        group.MapGet("/patient/{patientId:guid}", async (Guid patientId, IAppointmentRepository repo) =>
            Results.Ok(await repo.GetByPatientAsync(patientId)));

        // Get appointments by doctor for a date
        group.MapGet("/doctor/{doctorId:guid}", async (Guid doctorId, DateTime? date, IAppointmentRepository repo) =>
            Results.Ok(await repo.GetByDoctorAsync(doctorId, date ?? DateTime.UtcNow.Date)));

        // Create appointment
        group.MapPost("/", async (CreateAppointmentRequest req, IAppointmentRepository repo, IUnitOfWork uow) =>
        {
            if (req.DoctorId.HasValue)
            {
                var hasConflict = await repo.HasConflictAsync(req.DoctorId.Value, req.ScheduledAt, req.DurationMinutes);
                if (hasConflict)
                    return Results.BadRequest("Doctor has a scheduling conflict at this time");
            }

            var apt = Appointment.Create(
                req.PatientId,
                req.ScheduledAt,
                req.Type,
                req.CycleId,
                req.DoctorId,
                req.DurationMinutes,
                req.Notes,
                req.RoomNumber);

            await repo.AddAsync(apt);
            await uow.SaveChangesAsync();
            return Results.Created($"/api/appointments/{apt.Id}", apt);
        });

        // Confirm appointment
        group.MapPost("/{id:guid}/confirm", async (Guid id, IAppointmentRepository repo, IUnitOfWork uow) =>
        {
            var apt = await repo.GetByIdAsync(id);
            if (apt is null) return Results.NotFound();
            apt.Confirm();
            await uow.SaveChangesAsync();
            return Results.Ok(apt);
        });

        // Check in
        group.MapPost("/{id:guid}/checkin", async (Guid id, IAppointmentRepository repo, IUnitOfWork uow) =>
        {
            var apt = await repo.GetByIdAsync(id);
            if (apt is null) return Results.NotFound();
            apt.CheckIn();
            await uow.SaveChangesAsync();
            return Results.Ok(apt);
        });

        // Complete
        group.MapPost("/{id:guid}/complete", async (Guid id, IAppointmentRepository repo, IUnitOfWork uow) =>
        {
            var apt = await repo.GetByIdAsync(id);
            if (apt is null) return Results.NotFound();
            apt.Complete();
            await uow.SaveChangesAsync();
            return Results.Ok(apt);
        });

        // Cancel
        group.MapPost("/{id:guid}/cancel", async (Guid id, CancelRequest? req, IAppointmentRepository repo, IUnitOfWork uow) =>
        {
            var apt = await repo.GetByIdAsync(id);
            if (apt is null) return Results.NotFound();
            apt.Cancel(req?.Reason);
            await uow.SaveChangesAsync();
            return Results.NoContent();
        });

        // Reschedule
        group.MapPost("/{id:guid}/reschedule", async (Guid id, RescheduleRequest req, IAppointmentRepository repo, IUnitOfWork uow) =>
        {
            var apt = await repo.GetByIdAsync(id);
            if (apt is null) return Results.NotFound();

            if (apt.DoctorId.HasValue)
            {
                var hasConflict = await repo.HasConflictAsync(apt.DoctorId.Value, req.NewDateTime, apt.DurationMinutes, id);
                if (hasConflict)
                    return Results.BadRequest("Doctor has a scheduling conflict at this time");
            }

            apt.Reschedule(req.NewDateTime);
            await uow.SaveChangesAsync();
            return Results.Ok(apt);
        });
    }

    public record CreateAppointmentRequest(
        Guid PatientId,
        DateTime ScheduledAt,
        AppointmentType Type,
        Guid? CycleId = null,
        Guid? DoctorId = null,
        int DurationMinutes = 30,
        string? Notes = null,
        string? RoomNumber = null);

    public record CancelRequest(string? Reason);
    public record RescheduleRequest(DateTime NewDateTime);
}
