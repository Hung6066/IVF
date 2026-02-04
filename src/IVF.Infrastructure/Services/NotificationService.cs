using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;

namespace IVF.Infrastructure.Services;

public class NotificationService : INotificationService
{
    private readonly INotificationRepository _notificationRepo;
    private readonly IUnitOfWork _unitOfWork;

    public NotificationService(INotificationRepository notificationRepo, IUnitOfWork unitOfWork)
    {
        _notificationRepo = notificationRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task SendAppointmentReminderAsync(Guid doctorUserId, Guid appointmentId, DateTime scheduledAt, CancellationToken ct = default)
    {
        var notification = Notification.Create(
            doctorUserId,
            "📅 Lịch hẹn mới",
            $"Bạn có lịch hẹn mới lúc {scheduledAt:HH:mm dd/MM/yyyy}",
            NotificationType.AppointmentReminder,
            "Appointment",
            appointmentId
        );

        await _notificationRepo.AddAsync(notification, ct);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task SendQueueCalledAsync(Guid patientUserId, string ticketNumber, string departmentName, string? roomNumber = null, CancellationToken ct = default)
    {
        var roomInfo = roomNumber != null ? $" tại {roomNumber}" : "";
        var notification = Notification.Create(
            patientUserId,
            "🔔 Đến lượt khám",
            $"Số {ticketNumber} - {departmentName}{roomInfo}. Vui lòng vào phòng khám!",
            NotificationType.QueueCalled,
            "QueueTicket",
            null
        );

        await _notificationRepo.AddAsync(notification, ct);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task SendCycleUpdateAsync(Guid patientUserId, Guid cycleId, string status, string? message = null, CancellationToken ct = default)
    {
        var msg = message ?? $"Chu kỳ điều trị đã chuyển sang giai đoạn: {status}";
        var notification = Notification.Create(
            patientUserId,
            "🔄 Cập nhật chu kỳ",
            msg,
            NotificationType.CycleUpdate,
            "TreatmentCycle",
            cycleId
        );

        await _notificationRepo.AddAsync(notification, ct);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task SendPaymentDueAsync(Guid patientUserId, Guid invoiceId, decimal amount, DateTime dueDate, CancellationToken ct = default)
    {
        var notification = Notification.Create(
            patientUserId,
            "💰 Thanh toán đến hạn",
            $"Hóa đơn {amount:N0} VNĐ cần thanh toán trước {dueDate:dd/MM/yyyy}",
            NotificationType.PaymentDue,
            "Invoice",
            invoiceId
        );

        await _notificationRepo.AddAsync(notification, ct);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task SendInvoiceIssuedAsync(Guid patientUserId, Guid invoiceId, decimal totalAmount, CancellationToken ct = default)
    {
        var notification = Notification.Create(
            patientUserId,
            "🧾 Hóa đơn mới",
            $"Hóa đơn mới đã được tạo với tổng tiền {totalAmount:N0} VNĐ",
            NotificationType.Info,
            "Invoice",
            invoiceId
        );

        await _notificationRepo.AddAsync(notification, ct);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task SendNotificationAsync(Guid userId, string title, string message, NotificationType type, string? entityType = null, Guid? entityId = null, CancellationToken ct = default)
    {
        var notification = Notification.Create(
            userId,
            title,
            message,
            type,
            entityType,
            entityId
        );

        await _notificationRepo.AddAsync(notification, ct);
        await _unitOfWork.SaveChangesAsync(ct);
    }
}
