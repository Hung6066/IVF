using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace IVF.API.Hubs;

/// <summary>
/// SignalR hub for real-time backup/restore operation log streaming.
/// Clients join an operation-specific group to receive log lines.
/// </summary>
[Authorize(Policy = "AdminOnly")]
public class BackupHub : Hub
{
    public async Task JoinOperation(string operationId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, operationId);
    }

    public async Task LeaveOperation(string operationId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, operationId);
    }

    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
    }
}
