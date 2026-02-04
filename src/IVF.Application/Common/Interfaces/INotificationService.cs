using IVF.Domain.Entities;

namespace IVF.Application.Common.Interfaces;

/// <summary>
/// Centralized notification service for consistent notification handling across all services
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Send appointment reminder notification to doctor
    /// </summary>
    Task SendAppointmentReminderAsync(Guid doctorUserId, Guid appointmentId, DateTime scheduledAt, CancellationToken ct = default);

    /// <summary>
    /// Send queue ticket called notification to patient
    /// </summary>
    Task SendQueueCalledAsync(Guid patientUserId, string ticketNumber, string departmentName, string? roomNumber = null, CancellationToken ct = default);

    /// <summary>
    /// Send cycle status update notification
    /// </summary>
    Task SendCycleUpdateAsync(Guid patientUserId, Guid cycleId, string status, string? message = null, CancellationToken ct = default);

    /// <summary>
    /// Send payment due notification
    /// </summary>
    Task SendPaymentDueAsync(Guid patientUserId, Guid invoiceId, decimal amount, DateTime dueDate, CancellationToken ct = default);

    /// <summary>
    /// Send invoice issued notification
    /// </summary>
    Task SendInvoiceIssuedAsync(Guid patientUserId, Guid invoiceId, decimal totalAmount, CancellationToken ct = default);

    /// <summary>
    /// Send generic notification
    /// </summary>
    Task SendNotificationAsync(Guid userId, string title, string message, NotificationType type, string? entityType = null, Guid? entityId = null, CancellationToken ct = default);
}
