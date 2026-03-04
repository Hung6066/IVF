using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace IVF.API.Hubs;

/// <summary>
/// SignalR hub for real-time evidence collection progress streaming.
/// Clients join an operation-specific group to receive log lines and progress updates.
/// </summary>
[Authorize]
public class EvidenceHub : Hub
{
    public async Task JoinOperation(string operationId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, operationId);
    }

    public async Task LeaveOperation(string operationId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, operationId);
    }
}
