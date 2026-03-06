using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace IVF.API.Hubs;

/// <summary>
/// SignalR hub for real-time VPS infrastructure metrics streaming.
/// Pushes CPU, RAM, disk, Swarm status, health checks, and alerts every 5 seconds.
/// </summary>
[Authorize(Policy = "AdminOnly")]
public class InfrastructureHub : Hub
{
    /// <summary>Client joins real-time metrics stream.</summary>
    public async Task JoinMonitoring()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "infra-monitoring");
    }

    /// <summary>Client leaves real-time metrics stream.</summary>
    public async Task LeaveMonitoring()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "infra-monitoring");
    }

    public override async Task OnConnectedAsync()
    {
        // Auto-join monitoring group on connect
        await Groups.AddToGroupAsync(Context.ConnectionId, "infra-monitoring");
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "infra-monitoring");
        await base.OnDisconnectedAsync(exception);
    }
}
