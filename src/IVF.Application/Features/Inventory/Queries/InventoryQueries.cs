using IVF.Application.Common;
using IVF.Application.Common.Interfaces;
using IVF.Application.Features.Inventory.Commands;
using MediatR;

namespace IVF.Application.Features.Inventory.Queries;

// === Search Items ===
public record SearchInventoryItemsQuery(string? Query, string? Category, bool? LowStockOnly, int Page = 1, int PageSize = 20)
    : IRequest<PagedResult<InventoryItemDto>>;

public class SearchInventoryItemsHandler(IInventoryItemRepository repo)
    : IRequestHandler<SearchInventoryItemsQuery, PagedResult<InventoryItemDto>>
{
    public async Task<PagedResult<InventoryItemDto>> Handle(SearchInventoryItemsQuery r, CancellationToken ct)
    {
        var (items, total) = await repo.SearchAsync(r.Query, r.Category, r.LowStockOnly, r.Page, r.PageSize, ct);
        var dtos = items.Select(InventoryMapper.ToDto).ToList();
        return new PagedResult<InventoryItemDto>(dtos, total, r.Page, r.PageSize);
    }
}

// === Get Item By Id ===
public record GetInventoryItemByIdQuery(Guid Id) : IRequest<Result<InventoryItemDto>>;

public class GetInventoryItemByIdHandler(IInventoryItemRepository repo)
    : IRequestHandler<GetInventoryItemByIdQuery, Result<InventoryItemDto>>
{
    public async Task<Result<InventoryItemDto>> Handle(GetInventoryItemByIdQuery request, CancellationToken ct)
    {
        var item = await repo.GetByIdAsync(request.Id, ct);
        if (item == null) return Result<InventoryItemDto>.Failure("Không tìm thấy thuốc/vật tư");
        return Result<InventoryItemDto>.Success(InventoryMapper.ToDto(item));
    }
}

// === Get Low Stock Alerts ===
public record GetLowStockAlertsQuery() : IRequest<IReadOnlyList<InventoryItemDto>>;

public class GetLowStockAlertsHandler(IInventoryItemRepository repo)
    : IRequestHandler<GetLowStockAlertsQuery, IReadOnlyList<InventoryItemDto>>
{
    public async Task<IReadOnlyList<InventoryItemDto>> Handle(GetLowStockAlertsQuery request, CancellationToken ct)
    {
        var items = await repo.GetLowStockAsync(ct);
        return items.Select(InventoryMapper.ToDto).ToList();
    }
}

// === Get Expiring Items ===
public record GetExpiringItemsQuery(int Days = 30) : IRequest<IReadOnlyList<InventoryItemDto>>;

public class GetExpiringItemsHandler(IInventoryItemRepository repo)
    : IRequestHandler<GetExpiringItemsQuery, IReadOnlyList<InventoryItemDto>>
{
    public async Task<IReadOnlyList<InventoryItemDto>> Handle(GetExpiringItemsQuery request, CancellationToken ct)
    {
        var items = await repo.GetExpiringAsync(request.Days, ct);
        return items.Select(InventoryMapper.ToDto).ToList();
    }
}

// === Get Transaction History ===
public record GetStockTransactionsQuery(Guid ItemId, int Page = 1, int PageSize = 20)
    : IRequest<IReadOnlyList<StockTransactionDto>>;

public class GetStockTransactionsHandler(IInventoryItemRepository repo)
    : IRequestHandler<GetStockTransactionsQuery, IReadOnlyList<StockTransactionDto>>
{
    public async Task<IReadOnlyList<StockTransactionDto>> Handle(GetStockTransactionsQuery request, CancellationToken ct)
    {
        var txs = await repo.GetTransactionsAsync(request.ItemId, request.Page, request.PageSize, ct);
        return txs.Select(InventoryMapper.ToDto).ToList();
    }
}
