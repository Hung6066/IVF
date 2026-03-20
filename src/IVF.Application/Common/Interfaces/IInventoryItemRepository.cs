using IVF.Domain.Entities;

namespace IVF.Application.Common.Interfaces;

public interface IInventoryItemRepository
{
    Task<InventoryItem?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<InventoryItem?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<(IReadOnlyList<InventoryItem> Items, int Total)> SearchAsync(string? query, string? category, bool? lowStockOnly, int page, int pageSize, CancellationToken ct = default);
    Task<IReadOnlyList<InventoryItem>> GetLowStockAsync(CancellationToken ct = default);
    Task<IReadOnlyList<InventoryItem>> GetExpiringAsync(int days, CancellationToken ct = default);
    Task<InventoryItem> AddAsync(InventoryItem item, CancellationToken ct = default);
    Task UpdateAsync(InventoryItem item, CancellationToken ct = default);
    Task<IReadOnlyList<StockTransaction>> GetTransactionsAsync(Guid itemId, int page, int pageSize, CancellationToken ct = default);
    Task<StockTransaction> AddTransactionAsync(StockTransaction transaction, CancellationToken ct = default);
}
