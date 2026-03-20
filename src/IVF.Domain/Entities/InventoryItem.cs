using IVF.Domain.Common;

namespace IVF.Domain.Entities;

/// <summary>
/// Clinical inventory item — drugs and medical supplies (VTTH) in stock.
/// </summary>
public class InventoryItem : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; private set; }
    public void SetTenantId(Guid tenantId) { TenantId = tenantId; SetUpdated(); }

    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? GenericName { get; private set; }
    public string Category { get; private set; } = string.Empty; // Drug, Supply, Consumable, Reagent
    public string Unit { get; private set; } = string.Empty; // viên, ống, ml, cái, hộp
    public string? Manufacturer { get; private set; }
    public string? Supplier { get; private set; }

    // Stock levels
    public int CurrentStock { get; private set; }
    public int MinStock { get; private set; }
    public int MaxStock { get; private set; }
    public decimal UnitPrice { get; private set; }

    // Expiry tracking
    public DateTime? ExpiryDate { get; private set; }
    public string? BatchNumber { get; private set; }
    public string? StorageLocation { get; private set; } // Kho chính, Tủ trực, Tủ lạnh

    public bool IsActive { get; private set; } = true;
    public string? Notes { get; private set; }

    // Transactions
    public virtual ICollection<StockTransaction> Transactions { get; private set; } = new List<StockTransaction>();

    private InventoryItem() { }

    public static InventoryItem Create(
        string code, string name, string category, string unit,
        int minStock, int maxStock, decimal unitPrice,
        string? genericName = null, string? manufacturer = null, string? supplier = null,
        DateTime? expiryDate = null, string? batchNumber = null, string? storageLocation = null)
    {
        return new InventoryItem
        {
            Code = code,
            Name = name,
            Category = category,
            Unit = unit,
            CurrentStock = 0,
            MinStock = minStock,
            MaxStock = maxStock,
            UnitPrice = unitPrice,
            GenericName = genericName,
            Manufacturer = manufacturer,
            Supplier = supplier,
            ExpiryDate = expiryDate,
            BatchNumber = batchNumber,
            StorageLocation = storageLocation
        };
    }

    public void UpdateInfo(string name, string category, string unit, int minStock, int maxStock,
        decimal unitPrice, string? genericName, string? manufacturer, string? supplier,
        DateTime? expiryDate, string? batchNumber, string? storageLocation, string? notes)
    {
        Name = name;
        Category = category;
        Unit = unit;
        MinStock = minStock;
        MaxStock = maxStock;
        UnitPrice = unitPrice;
        GenericName = genericName;
        Manufacturer = manufacturer;
        Supplier = supplier;
        ExpiryDate = expiryDate;
        BatchNumber = batchNumber;
        StorageLocation = storageLocation;
        Notes = notes;
        SetUpdated();
    }

    public void AddStock(int quantity)
    {
        CurrentStock += quantity;
        SetUpdated();
    }

    public bool RemoveStock(int quantity)
    {
        if (CurrentStock < quantity) return false;
        CurrentStock -= quantity;
        SetUpdated();
        return true;
    }

    public void Deactivate()
    {
        IsActive = false;
        SetUpdated();
    }

    public void Activate()
    {
        IsActive = true;
        SetUpdated();
    }

    public bool IsLowStock => CurrentStock <= MinStock;
    public bool IsExpired => ExpiryDate.HasValue && ExpiryDate.Value.Date <= DateTime.UtcNow.Date;
    public bool IsNearExpiry => ExpiryDate.HasValue && ExpiryDate.Value.Date <= DateTime.UtcNow.AddDays(30).Date;
}

/// <summary>
/// Records every stock movement (import, export, usage, adjustment).
/// </summary>
public class StockTransaction : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; private set; }
    public void SetTenantId(Guid tenantId) { TenantId = tenantId; SetUpdated(); }

    public Guid ItemId { get; private set; }
    public string TransactionType { get; private set; } = string.Empty; // Import, Export, Usage, Adjustment, Return
    public int Quantity { get; private set; }
    public int StockAfter { get; private set; }

    public string? Reference { get; private set; } // PO number, prescription ID, etc.
    public string? Reason { get; private set; }
    public Guid? PerformedByUserId { get; private set; }
    public string? PerformedByName { get; private set; }

    // For import transactions
    public string? SupplierName { get; private set; }
    public decimal? UnitCost { get; private set; }
    public string? BatchNumber { get; private set; }

    public virtual InventoryItem Item { get; private set; } = null!;

    private StockTransaction() { }

    public static StockTransaction Create(
        Guid itemId, string transactionType, int quantity, int stockAfter,
        string? reference = null, string? reason = null,
        Guid? performedByUserId = null, string? performedByName = null,
        string? supplierName = null, decimal? unitCost = null, string? batchNumber = null)
    {
        return new StockTransaction
        {
            ItemId = itemId,
            TransactionType = transactionType,
            Quantity = quantity,
            StockAfter = stockAfter,
            Reference = reference,
            Reason = reason,
            PerformedByUserId = performedByUserId,
            PerformedByName = performedByName,
            SupplierName = supplierName,
            UnitCost = unitCost,
            BatchNumber = batchNumber
        };
    }
}
