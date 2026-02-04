using Microsoft.AspNetCore.SignalR;

namespace IVF.API.Hubs;

/// <summary>
/// SignalR hub for real-time queue updates
/// Clients join department-specific groups to receive relevant updates
/// </summary>
public class QueueHub : Hub
{
    /// <summary>
    /// Join a department group to receive queue updates for that department
    /// </summary>
    public async Task JoinDepartment(string departmentCode)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"dept_{departmentCode}");
    }

    /// <summary>
    /// Leave a department group
    /// </summary>
    public async Task LeaveDepartment(string departmentCode)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"dept_{departmentCode}");
    }
}

/// <summary>
/// Service to broadcast queue updates to connected clients
/// </summary>
public interface IQueueNotifier
{
    Task NotifyTicketIssued(string departmentCode, QueueTicketDto ticket);
    Task NotifyTicketCalled(string departmentCode, QueueTicketDto ticket);
    Task NotifyTicketCompleted(string departmentCode, Guid ticketId);
    Task NotifyTicketSkipped(string departmentCode, Guid ticketId);
}

public class QueueNotifier : IQueueNotifier
{
    private readonly IHubContext<QueueHub> _hubContext;

    public QueueNotifier(IHubContext<QueueHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task NotifyTicketIssued(string departmentCode, QueueTicketDto ticket)
    {
        await _hubContext.Clients.Group($"dept_{departmentCode}")
            .SendAsync("TicketIssued", ticket);
    }

    public async Task NotifyTicketCalled(string departmentCode, QueueTicketDto ticket)
    {
        await _hubContext.Clients.Group($"dept_{departmentCode}")
            .SendAsync("TicketCalled", ticket);
    }

    public async Task NotifyTicketCompleted(string departmentCode, Guid ticketId)
    {
        await _hubContext.Clients.Group($"dept_{departmentCode}")
            .SendAsync("TicketCompleted", ticketId);
    }

    public async Task NotifyTicketSkipped(string departmentCode, Guid ticketId)
    {
        await _hubContext.Clients.Group($"dept_{departmentCode}")
            .SendAsync("TicketSkipped", ticketId);
    }
}

// DTO for queue ticket notifications
public record QueueTicketDto(
    Guid Id,
    string TicketNumber,
    string? PatientName,
    string DepartmentCode,
    string Status,
    DateTime IssuedAt,
    DateTime? CalledAt
);
