using IVF.Application.Features.Inventory.Commands;
using IVF.Application.Features.Inventory.Queries;
using MediatR;

namespace IVF.API.Endpoints;

public static class InventoryEndpoints
{
    public static void MapInventoryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/inventory").WithTags("Inventory").RequireAuthorization();

        // Search items
        group.MapGet("/", async (IMediator m, string? q, string? category, bool? lowStockOnly, int page = 1, int pageSize = 20) =>
            Results.Ok(await m.Send(new SearchInventoryItemsQuery(q, category, lowStockOnly, page, pageSize))));

        // Get item by ID
        group.MapGet("/{id:guid}", async (Guid id, IMediator m) =>
        {
            var r = await m.Send(new GetInventoryItemByIdQuery(id));
            return r.IsSuccess ? Results.Ok(r.Value) : Results.NotFound(r.Error);
        });

        // Get low stock alerts
        group.MapGet("/alerts/low-stock", async (IMediator m) =>
            Results.Ok(await m.Send(new GetLowStockAlertsQuery())));

        // Get expiring items
        group.MapGet("/alerts/expiring", async (IMediator m, int days = 30) =>
            Results.Ok(await m.Send(new GetExpiringItemsQuery(days))));

        // Get transaction history
        group.MapGet("/{id:guid}/transactions", async (Guid id, IMediator m, int page = 1, int pageSize = 20) =>
            Results.Ok(await m.Send(new GetStockTransactionsQuery(id, page, pageSize))));

        // Create item
        group.MapPost("/", async (CreateInventoryItemCommand cmd, IMediator m) =>
        {
            var r = await m.Send(cmd);
            return r.IsSuccess ? Results.Created($"/api/inventory/{r.Value!.Id}", r.Value) : Results.BadRequest(r.Error);
        });

        // Update item
        group.MapPut("/{id:guid}", async (Guid id, UpdateInventoryItemRequest req, IMediator m) =>
        {
            var r = await m.Send(new UpdateInventoryItemCommand(id, req.Name, req.Category, req.Unit,
                req.MinStock, req.MaxStock, req.UnitPrice, req.GenericName, req.Manufacturer,
                req.Supplier, req.ExpiryDate, req.BatchNumber, req.StorageLocation, req.Notes));
            return r.IsSuccess ? Results.Ok(r.Value) : Results.NotFound(r.Error);
        });

        // Import stock
        group.MapPost("/import", async (ImportStockCommand cmd, IMediator m) =>
        {
            var r = await m.Send(cmd);
            return r.IsSuccess ? Results.Ok(r.Value) : Results.BadRequest(r.Error);
        });

        // Record usage
        group.MapPost("/usage", async (RecordUsageCommand cmd, IMediator m) =>
        {
            var r = await m.Send(cmd);
            return r.IsSuccess ? Results.Ok(r.Value) : Results.BadRequest(r.Error);
        });

        // Adjust stock
        group.MapPost("/adjust", async (AdjustStockCommand cmd, IMediator m) =>
        {
            var r = await m.Send(cmd);
            return r.IsSuccess ? Results.Ok(r.Value) : Results.BadRequest(r.Error);
        });
    }
}

// Request DTOs
public record UpdateInventoryItemRequest(
    string Name, string Category, string Unit,
    int MinStock, int MaxStock, decimal UnitPrice,
    string? GenericName, string? Manufacturer, string? Supplier,
    DateTime? ExpiryDate, string? BatchNumber, string? StorageLocation, string? Notes);
