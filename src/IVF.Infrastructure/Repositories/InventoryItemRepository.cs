using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IVF.Infrastructure.Repositories;

public class InventoryItemRepository(IvfDbContext context) : IInventoryItemRepository
{
    public async Task<InventoryItem?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await context.InventoryItems.FirstOrDefaultAsync(i => i.Id == id, ct);

    public async Task<InventoryItem?> GetByCodeAsync(string code, CancellationToken ct = default)
        => await context.InventoryItems.FirstOrDefaultAsync(i => i.Code == code, ct);

    public async Task<(IReadOnlyList<InventoryItem> Items, int Total)> SearchAsync(
        string? query, string? category, bool? lowStockOnly, int page, int pageSize, CancellationToken ct = default)
    {
        var q = context.InventoryItems.Where(i => i.IsActive).AsQueryable();

        if (!string.IsNullOrWhiteSpace(query))
            q = q.Where(i => i.Code.Contains(query) || i.Name.Contains(query) || (i.GenericName != null && i.GenericName.Contains(query)));
        if (!string.IsNullOrWhiteSpace(category))
            q = q.Where(i => i.Category == category);
        if (lowStockOnly == true)
            q = q.Where(i => i.CurrentStock <= i.MinStock);

        var total = await q.CountAsync(ct);
        var items = await q.OrderBy(i => i.Name)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return (items, total);
    }

    public async Task<IReadOnlyList<InventoryItem>> GetLowStockAsync(CancellationToken ct = default)
        => await context.InventoryItems
            .Where(i => i.IsActive && i.CurrentStock <= i.MinStock)
            .OrderBy(i => i.CurrentStock)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<InventoryItem>> GetExpiringAsync(int days, CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(days);
        return await context.InventoryItems
            .Where(i => i.IsActive && i.ExpiryDate != null && i.ExpiryDate <= cutoff)
            .OrderBy(i => i.ExpiryDate)
            .ToListAsync(ct);
    }

    public async Task<InventoryItem> AddAsync(InventoryItem item, CancellationToken ct = default)
    {
        await context.InventoryItems.AddAsync(item, ct);
        return item;
    }

    public Task UpdateAsync(InventoryItem item, CancellationToken ct = default)
    {
        context.InventoryItems.Update(item);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<StockTransaction>> GetTransactionsAsync(Guid itemId, int page, int pageSize, CancellationToken ct = default)
        => await context.StockTransactions
            .Include(t => t.Item)
            .Where(t => t.ItemId == itemId)
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);

    public async Task<StockTransaction> AddTransactionAsync(StockTransaction transaction, CancellationToken ct = default)
    {
        await context.StockTransactions.AddAsync(transaction, ct);
        return transaction;
    }
}
