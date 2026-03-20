using IVF.Application.Features.Appointments.Commands;
using IVF.Application.Features.Appointments.Queries;
using IVF.Domain.Enums;
using MediatR;

namespace IVF.API.Endpoints;

public static class AppointmentEndpoints
{
    public static void MapAppointmentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/appointments").WithTags("Appointments").RequireAuthorization();

        // Get today's appointments
        group.MapGet("/today", async (IMediator mediator) =>
        {
            var result = await mediator.Send(new GetTodayAppointmentsQuery());
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        });

        // Get appointments by date range
        group.MapGet("/", async (IMediator mediator, DateTime? start, DateTime? end) =>
        {
            var result = await mediator.Send(new GetAppointmentsByDateRangeQuery(start, end));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        });

        // Get upcoming appointments
        group.MapGet("/upcoming", async (IMediator mediator, Guid? doctorId, int days = 7) =>
        {
            var result = await mediator.Send(new GetUpcomingAppointmentsQuery(doctorId, days));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        });

        // Get appointment by ID
        group.MapGet("/{id:guid}", async (Guid id, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetAppointmentByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        });

        // Get appointments by patient
        group.MapGet("/patient/{patientId:guid}", async (Guid patientId, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetAppointmentsByPatientQuery(patientId));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        });

        // Get appointments by doctor for a date
        group.MapGet("/doctor/{doctorId:guid}", async (Guid doctorId, DateTime? date, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetAppointmentsByDoctorQuery(doctorId, date));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        });

        // Create appointment
        group.MapPost("/", async (CreateAppointmentCommand cmd, IMediator mediator) =>
        {
            var result = await mediator.Send(cmd);
            return result.IsSuccess
                ? Results.Created($"/api/appointments/{result.Value!.Id}", result.Value)
                : Results.BadRequest(result.Error);
        });

        // Confirm appointment
        group.MapPost("/{id:guid}/confirm", async (Guid id, IMediator mediator) =>
        {
            var result = await mediator.Send(new ConfirmAppointmentCommand(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        });

        // Check in
        group.MapPost("/{id:guid}/checkin", async (Guid id, IMediator mediator) =>
        {
            var result = await mediator.Send(new CheckInAppointmentCommand(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        });

        // Complete
        group.MapPost("/{id:guid}/complete", async (Guid id, IMediator mediator) =>
        {
            var result = await mediator.Send(new CompleteAppointmentCommand(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        });

        // Cancel
        group.MapPost("/{id:guid}/cancel", async (Guid id, CancelRequest? req, IMediator mediator) =>
        {
            var result = await mediator.Send(new CancelAppointmentCommand(id, req?.Reason));
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        });

        // Reschedule
        group.MapPost("/{id:guid}/reschedule", async (Guid id, RescheduleRequest req, IMediator mediator) =>
        {
            var result = await mediator.Send(new RescheduleAppointmentCommand(id, req.NewDateTime));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        });
    }

    public record CancelRequest(string? Reason);
    public record RescheduleRequest(DateTime NewDateTime);
}
