using FluentValidation;
using IVF.Application.Common;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using MediatR;

namespace IVF.Application.Features.Inventory.Commands;

// === DTOs ===

public record InventoryItemDto(
    Guid Id,
    string Code,
    string Name,
    string? GenericName,
    string Category,
    string Unit,
    string? Manufacturer,
    string? Supplier,
    int CurrentStock,
    int MinStock,
    int MaxStock,
    decimal UnitPrice,
    DateTime? ExpiryDate,
    string? BatchNumber,
    string? StorageLocation,
    bool IsActive,
    bool IsLowStock,
    bool IsExpired,
    bool IsNearExpiry,
    string? Notes,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record StockTransactionDto(
    Guid Id,
    Guid ItemId,
    string? ItemName,
    string TransactionType,
    int Quantity,
    int StockAfter,
    string? Reference,
    string? Reason,
    string? PerformedByName,
    string? SupplierName,
    decimal? UnitCost,
    string? BatchNumber,
    DateTime CreatedAt);

// === Mapper ===

internal static class InventoryMapper
{
    public static InventoryItemDto ToDto(InventoryItem i) => new(
        i.Id, i.Code, i.Name, i.GenericName, i.Category, i.Unit,
        i.Manufacturer, i.Supplier,
        i.CurrentStock, i.MinStock, i.MaxStock, i.UnitPrice,
        i.ExpiryDate, i.BatchNumber, i.StorageLocation,
        i.IsActive, i.IsLowStock, i.IsExpired, i.IsNearExpiry,
        i.Notes, i.CreatedAt, i.UpdatedAt);

    public static StockTransactionDto ToDto(StockTransaction t) => new(
        t.Id, t.ItemId, t.Item?.Name, t.TransactionType,
        t.Quantity, t.StockAfter, t.Reference, t.Reason,
        t.PerformedByName, t.SupplierName, t.UnitCost, t.BatchNumber,
        t.CreatedAt);
}

// === Create Item ===

public record CreateInventoryItemCommand(
    string Code,
    string Name,
    string Category,
    string Unit,
    int MinStock,
    int MaxStock,
    decimal UnitPrice,
    string? GenericName = null,
    string? Manufacturer = null,
    string? Supplier = null,
    DateTime? ExpiryDate = null,
    string? BatchNumber = null,
    string? StorageLocation = null) : IRequest<Result<InventoryItemDto>>;

public class CreateInventoryItemValidator : AbstractValidator<CreateInventoryItemCommand>
{
    public CreateInventoryItemValidator()
    {
        RuleFor(x => x.Code).NotEmpty().WithMessage("Vui lòng nhập mã thuốc/vật tư");
        RuleFor(x => x.Name).NotEmpty().WithMessage("Vui lòng nhập tên thuốc/vật tư");
        RuleFor(x => x.Category).NotEmpty().WithMessage("Vui lòng chọn danh mục");
        RuleFor(x => x.Unit).NotEmpty().WithMessage("Vui lòng chọn đơn vị tính");
        RuleFor(x => x.MinStock).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MaxStock).GreaterThan(0);
        RuleFor(x => x.UnitPrice).GreaterThanOrEqualTo(0);
    }
}

public class CreateInventoryItemHandler(IInventoryItemRepository repo, IUnitOfWork unitOfWork)
    : IRequestHandler<CreateInventoryItemCommand, Result<InventoryItemDto>>
{
    public async Task<Result<InventoryItemDto>> Handle(CreateInventoryItemCommand r, CancellationToken ct)
    {
        var existing = await repo.GetByCodeAsync(r.Code, ct);
        if (existing != null) return Result<InventoryItemDto>.Failure("Mã thuốc/vật tư đã tồn tại");

        var item = InventoryItem.Create(r.Code, r.Name, r.Category, r.Unit,
            r.MinStock, r.MaxStock, r.UnitPrice,
            r.GenericName, r.Manufacturer, r.Supplier,
            r.ExpiryDate, r.BatchNumber, r.StorageLocation);

        await repo.AddAsync(item, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return Result<InventoryItemDto>.Success(InventoryMapper.ToDto(item));
    }
}

// === Update Item ===

public record UpdateInventoryItemCommand(
    Guid Id,
    string Name,
    string Category,
    string Unit,
    int MinStock,
    int MaxStock,
    decimal UnitPrice,
    string? GenericName,
    string? Manufacturer,
    string? Supplier,
    DateTime? ExpiryDate,
    string? BatchNumber,
    string? StorageLocation,
    string? Notes) : IRequest<Result<InventoryItemDto>>;

public class UpdateInventoryItemHandler(IInventoryItemRepository repo, IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateInventoryItemCommand, Result<InventoryItemDto>>
{
    public async Task<Result<InventoryItemDto>> Handle(UpdateInventoryItemCommand r, CancellationToken ct)
    {
        var item = await repo.GetByIdAsync(r.Id, ct);
        if (item == null) return Result<InventoryItemDto>.Failure("Không tìm thấy thuốc/vật tư");

        item.UpdateInfo(r.Name, r.Category, r.Unit, r.MinStock, r.MaxStock, r.UnitPrice,
            r.GenericName, r.Manufacturer, r.Supplier, r.ExpiryDate, r.BatchNumber, r.StorageLocation, r.Notes);
        await repo.UpdateAsync(item, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return Result<InventoryItemDto>.Success(InventoryMapper.ToDto(item));
    }
}

// === Import Stock (Nhập kho) ===

public record ImportStockCommand(
    Guid ItemId,
    int Quantity,
    string? SupplierName,
    decimal? UnitCost,
    string? BatchNumber,
    string? Reference,
    string? PerformedByName) : IRequest<Result<StockTransactionDto>>;

public class ImportStockValidator : AbstractValidator<ImportStockCommand>
{
    public ImportStockValidator()
    {
        RuleFor(x => x.ItemId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("Số lượng nhập phải lớn hơn 0");
    }
}

public class ImportStockHandler(IInventoryItemRepository repo, IUnitOfWork unitOfWork)
    : IRequestHandler<ImportStockCommand, Result<StockTransactionDto>>
{
    public async Task<Result<StockTransactionDto>> Handle(ImportStockCommand r, CancellationToken ct)
    {
        var item = await repo.GetByIdAsync(r.ItemId, ct);
        if (item == null) return Result<StockTransactionDto>.Failure("Không tìm thấy thuốc/vật tư");

        item.AddStock(r.Quantity);
        var tx = StockTransaction.Create(r.ItemId, "Import", r.Quantity, item.CurrentStock,
            r.Reference, null, null, r.PerformedByName, r.SupplierName, r.UnitCost, r.BatchNumber);

        await repo.AddTransactionAsync(tx, ct);
        await repo.UpdateAsync(item, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return Result<StockTransactionDto>.Success(InventoryMapper.ToDto(tx));
    }
}

// === Record Usage (Xuất kho / Sử dụng) ===

public record RecordUsageCommand(
    Guid ItemId,
    int Quantity,
    string? Reference,
    string? Reason,
    string? PerformedByName) : IRequest<Result<StockTransactionDto>>;

public class RecordUsageValidator : AbstractValidator<RecordUsageCommand>
{
    public RecordUsageValidator()
    {
        RuleFor(x => x.ItemId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("Số lượng xuất phải lớn hơn 0");
    }
}

public class RecordUsageHandler(IInventoryItemRepository repo, IUnitOfWork unitOfWork)
    : IRequestHandler<RecordUsageCommand, Result<StockTransactionDto>>
{
    public async Task<Result<StockTransactionDto>> Handle(RecordUsageCommand r, CancellationToken ct)
    {
        var item = await repo.GetByIdAsync(r.ItemId, ct);
        if (item == null) return Result<StockTransactionDto>.Failure("Không tìm thấy thuốc/vật tư");

        if (!item.RemoveStock(r.Quantity))
            return Result<StockTransactionDto>.Failure($"Không đủ tồn kho. Hiện có: {item.CurrentStock}");

        var tx = StockTransaction.Create(r.ItemId, "Usage", -r.Quantity, item.CurrentStock,
            r.Reference, r.Reason, null, r.PerformedByName);

        await repo.AddTransactionAsync(tx, ct);
        await repo.UpdateAsync(item, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return Result<StockTransactionDto>.Success(InventoryMapper.ToDto(tx));
    }
}

// === Adjust Stock (Kiểm kê / Điều chỉnh) ===

public record AdjustStockCommand(
    Guid ItemId,
    int NewQuantity,
    string Reason,
    string? PerformedByName) : IRequest<Result<StockTransactionDto>>;

public class AdjustStockHandler(IInventoryItemRepository repo, IUnitOfWork unitOfWork)
    : IRequestHandler<AdjustStockCommand, Result<StockTransactionDto>>
{
    public async Task<Result<StockTransactionDto>> Handle(AdjustStockCommand r, CancellationToken ct)
    {
        var item = await repo.GetByIdAsync(r.ItemId, ct);
        if (item == null) return Result<StockTransactionDto>.Failure("Không tìm thấy thuốc/vật tư");

        var diff = r.NewQuantity - item.CurrentStock;
        if (diff > 0) item.AddStock(diff);
        else if (diff < 0) item.RemoveStock(-diff);

        var tx = StockTransaction.Create(r.ItemId, "Adjustment", diff, item.CurrentStock,
            null, r.Reason, null, r.PerformedByName);

        await repo.AddTransactionAsync(tx, ct);
        await repo.UpdateAsync(item, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return Result<StockTransactionDto>.Success(InventoryMapper.ToDto(tx));
    }
}
